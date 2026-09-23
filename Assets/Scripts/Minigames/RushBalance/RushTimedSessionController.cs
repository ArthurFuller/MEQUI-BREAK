using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeviceScreen = UnityEngine.Device.Screen;

/// <summary>Controla a partida única e cronometrada do Rush Balance.</summary>
[DisallowMultipleComponent]
public sealed class RushTimedSessionController : MonoBehaviour
{
    [Header("Partida")]
    [SerializeField, Min(1f)] private float matchSeconds = 60f;
    [SerializeField] private string activityId = "rush_balance";

    [Header("Objetos existentes na Hierarchy")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text scoreLabel;
    [SerializeField] private CanvasGroup gameplayInput;
    [SerializeField] private ResultPopup resultPopup;
    [SerializeField] private SceneLoader sceneLoader;

    private float remaining;
    private int delivered;
    private int missed;
    private int gameplayControllers;
    private bool gameplayConfigured;
    private bool playing;
    private bool closing;
    private bool leaving;
    private bool logged;
    private bool settled;

    public bool IsPlaying => playing && !leaving && !settled;
    public bool IsFinishingCycle => closing && IsPlaying;
    public float Remaining => remaining;
    public event Action MatchStarted;
    public event Action TimeExpired;

    public void RegisterGameplay(bool configured)
    {
        gameplayControllers++;
        gameplayConfigured = configured;
    }

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(StartMatch);
        if (backButton != null) backButton.onClick.AddListener(BackToHub);
        SetInput(false);
        if (startPanel != null) startPanel.SetActive(true);
        RefreshHud();
    }

    private void OnEnable() => SceneLoader.SceneLeaving += HandleSceneLeaving;

    private void Start()
    {
        DeviceScreen.orientation = ScreenOrientation.Portrait;
        if (!ValidateSetup())
        {
            if (timerLabel != null) timerLabel.text = "Verifique o Console";
            return;
        }

        remaining = matchSeconds;
        RefreshHud();
    }

    private void Update()
    {
        if (!IsPlaying || closing || SceneLoader.IsTransitionInProgress) return;

        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        RefreshHud();
        if (remaining <= 0f) EndByTime();
    }

    public void StartMatch()
    {
        if (playing || closing || leaving || settled || !ValidateSetup()) return;

        delivered = 0;
        missed = 0;
        remaining = matchSeconds;
        playing = true;
        startPanel.SetActive(false);
        SetInput(true);
        EventLogger.Instance.BeginSession(activityId);
        logged = true;
        EventLogger.Instance.RecordUserAction("rush_start");
        RefreshHud();
        AudioManager.Instance?.PlayConfirm();
        MequiHaptics.Confirm();
        MatchStarted?.Invoke();
    }

    public void RecordOrder(bool wasDelivered)
    {
        if ((!playing && !closing) || settled) return;
        if (wasDelivered) delivered++;
        else missed++;
        EventLogger.Instance?.RecordActivityEvent(wasDelivered ? "rush_order_delivered" : "rush_order_missed");
        RefreshHud();
    }

    public void RecordReorder()
    {
        if (IsPlaying) EventLogger.Instance?.RecordUserAction("rush_reorder");
    }

    private void EndByTime()
    {
        if (closing || settled) return;
        closing = true;
        EventLogger.Instance?.MarkTimeLimitReached();

        // O cronômetro para, mas o Rush continua interativo até todos os clientes
        // que já estavam na tela concluírem seu ciclo e saírem completamente.
        TimeExpired?.Invoke();
        RefreshHud();
    }

    public void CompleteFinalCycle()
    {
        if (!closing || settled || leaving) return;

        playing = false;
        SetInput(false);
        FinishSession();
    }

    private void FinishSession()
    {
        if (settled) return;
        settled = true;
        closing = false;
        backButton.interactable = false;

        int points = PointsService.Instance.AwardParticipation();
        PlayerManager.Instance.SetPendingPoints(points, "RushBalance");
        EventLogger.Instance?.RecordActivityEvent($"result_delivered_{delivered}_missed_{missed}");
        EventLogger.Instance?.RecordActivityEvent($"participation_reward_{points}");
        EventLogger.Instance?.CompleteSession();
        logged = false;
        AudioManager.Instance?.PlayCompletion();
        MequiHaptics.Confirm();
        resultPopup.Show(points, delivered, missed);
    }

    public void BackToHub()
    {
        if (leaving || settled || SceneLoader.IsTransitionInProgress || sceneLoader == null) return;
        leaving = true;
        playing = false;
        SetInput(false);
        Abandon();
        sceneLoader.Load("HUB");
    }

    private void HandleSceneLeaving(Scene scene)
    {
        if (scene != gameObject.scene) return;
        leaving = true;
        playing = false;
        SetInput(false);
        Abandon();
    }

    private void Abandon()
    {
        if (!logged) return;
        logged = false;
        EventLogger.Instance?.AbandonSession();
    }

    private void RefreshHud()
    {
        if (timerLabel != null)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            timerLabel.SetText("{0}:{1:00}", seconds / 60, seconds % 60);
        }
        if (scoreLabel != null)
            scoreLabel.SetText("Entregues: {0}", delivered);
    }

    private void SetInput(bool value)
    {
        if (gameplayInput == null) return;
        gameplayInput.interactable = value;
        gameplayInput.blocksRaycasts = value;
    }

    private bool ValidateSetup()
    {
        string error =
            matchSeconds <= 0f ? "matchSeconds" :
            string.IsNullOrWhiteSpace(activityId) ? "activityId" :
            startPanel == null ? "startPanel" :
            startButton == null ? "startButton" :
            backButton == null ? "backButton" :
            timerLabel == null ? "timerLabel" :
            scoreLabel == null ? "scoreLabel" :
            gameplayInput == null ? "gameplayInput" :
            resultPopup == null ? "resultPopup" :
            sceneLoader == null ? "sceneLoader" :
            gameplayControllers != 1 ? $"controladores registrados: {gameplayControllers}; esperado: 1" :
            !gameplayConfigured ? "controlador do Rush inválido" :
            EventLogger.Instance == null ? "EventLogger; execute pelo Boot" :
            PointsService.Instance == null ? "PointsService; execute pelo Boot" :
            PlayerManager.Instance == null || PlayerManager.Instance.Profile == null
                ? "perfil do jogador; execute pelo Boot" : null;

        if (error == null) return true;
        Debug.LogError($"Rush Balance — sessão: configure {error}.", this);
        return false;
    }

    private void OnDisable()
    {
        SceneLoader.SceneLeaving -= HandleSceneLeaving;
        playing = false;
        SetInput(false);
        Abandon();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartMatch);
        if (backButton != null) backButton.onClick.RemoveListener(BackToHub);
    }
}
