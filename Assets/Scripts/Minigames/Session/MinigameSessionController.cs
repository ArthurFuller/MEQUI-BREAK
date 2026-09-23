using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeviceScreen = UnityEngine.Device.Screen;

/// <summary>Coordena os turnos e reutiliza os serviços inicializados pelo Boot.</summary>
[DisallowMultipleComponent]
public sealed class MinigameSessionController : MonoBehaviour
{
    [Header("Sessão")]
    [Tooltip("rush_balance ou combo_crew, conforme a cena.")]
    [SerializeField] private string activityId = "rush_balance";
    [Tooltip("Configure exatamente três valores. Rush: 4, 5, 6.")]
    [SerializeField] private int[] ordersPerTurn = { 4, 5, 6 };
    [SerializeField, Min(0f)] private float preparationSeconds = 10f;
    [Tooltip("Use LandscapeLeft ou LandscapeRight. O modo anterior é restaurado ao sair da cena.")]
    [SerializeField] private ScreenOrientation gameplayOrientation = ScreenOrientation.LandscapeLeft;

    [Header("Objetos existentes na Hierarchy")]
    [SerializeField] private GameObject preparationPanel;
    [SerializeField] private TMP_Text preparationLabel;
    [SerializeField] private TMP_Text turnLabel;
    [SerializeField] private Button startTurnButton;
    [SerializeField] private Button backButton;
    [Tooltip("Somente a área de jogo. Não incluir o resultado ou os botões de preparação.")]
    [SerializeField] private CanvasGroup gameplayInput;
    [SerializeField] private ResultPopup resultPopup;
    [SerializeField] private SceneLoader sceneLoader;

    private MinigameSessionState session;
    private float preparationRemaining;
    private int displayedSecond = -1;
    private bool loggedSession;
    private bool settled;
    private bool rewardGranted;
    private bool leaving;
    private int gameplayControllers;
    private bool gameplayConfigured;
    private bool orientationCaptured;
    private ScreenOrientation previousOrientation;

    public MinigameSessionState Session => session;
    public bool IsPlaying => session != null && !leaving && !SceneLoader.IsTransitionInProgress
        && session.CurrentPhase == MinigameSessionState.Phase.Playing;
    public bool CanArrange => session != null && !leaving && !SceneLoader.IsTransitionInProgress
        && (session.CurrentPhase == MinigameSessionState.Phase.Preparing || IsPlaying);
    public int MaximumOrders
    {
        get
        {
            if (ordersPerTurn == null || ordersPerTurn.Length != 3) return 0;
            int maximum = 0;
            foreach (int count in ordersPerTurn)
            {
                if (count < 1) return 0;
                maximum = Mathf.Max(maximum, count);
            }
            return maximum;
        }
    }

    // Exatamente um controlador de gameplay valida suas referências em Awake.
    public void RegisterGameplay(bool configured)
    {
        gameplayControllers++;
        gameplayConfigured = configured;
    }

    // Os controladores dos jogos assinam em OnEnable e removem em OnDisable.
    // Os índices de turno e de pedido são baseados em zero.
    public event Action<int, int> TurnPreparing;
    public event Action<int, int> TurnStarted;
    public event Action SessionFinished;

    private void Awake()
    {
        // Device.Screen também reproduz a rotação no Device Simulator do Unity.
        // Aplicar no Awake evita o primeiro frame do Canvas landscape em portrait.
        previousOrientation = DeviceScreen.orientation;
        orientationCaptured = true;
        DeviceScreen.orientation = gameplayOrientation;

        if (startTurnButton != null) startTurnButton.onClick.AddListener(StartTurn);
        if (backButton != null) backButton.onClick.AddListener(BackToHub);
        SetInput(false);
        if (preparationPanel != null) preparationPanel.SetActive(false);
    }

    private void OnEnable() => SceneLoader.SceneLeaving += HandleSceneLeaving;

    private void HandleSceneLeaving(Scene scene)
    {
        if (scene != gameObject.scene) return;
        leaving = true;
        Abandon();
        SetInput(false);
        RestoreOrientation();
    }

    private IEnumerator Start()
    {
        // O SceneLoader carrega a cena aditivamente antes de terminar a animação.
        while (SceneLoader.IsTransitionInProgress)
            yield return null;

        if (leaving) yield break;

        // A cena é landscape por definição. Forçamos a orientação antes da validação
        // para impedir que qualquer estado de erro seja exibido no Canvas portrait.
        DeviceScreen.orientation = gameplayOrientation;
        yield return null;
        yield return null;
        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (!leaving && (Application.isMobilePlatform || Application.isEditor) && DeviceScreen.width < DeviceScreen.height
            && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (leaving) yield break;
        Canvas.ForceUpdateCanvases();

        if (!ValidateSetup())
        {
            // Não desabilitar: OnDisable marca a saída e impediria o botão Voltar.
            // A sessão nula já mantém o Update e o início do turno inativos.
            if (turnLabel != null) turnLabel.text = "Falha ao iniciar. Consulte o Console.";
            if (backButton != null) backButton.interactable = true;
            yield break;
        }

        session = new MinigameSessionState(ordersPerTurn);
        EventLogger.Instance.BeginSession(activityId);
        loggedSession = true;
        PrepareTurn();
    }

    private void Update()
    {
        if (session == null || leaving || SceneLoader.IsTransitionInProgress) return;

        if (session.CurrentPhase == MinigameSessionState.Phase.BetweenTurns)
        {
            session.PrepareNextTurn();
            PrepareTurn();
            return;
        }

        if (session.CurrentPhase != MinigameSessionState.Phase.Preparing) return;
        preparationRemaining = Mathf.Max(0f, preparationRemaining - Time.deltaTime);
        RefreshPreparationLabel();
        if (preparationRemaining <= 0f) StartTurn();
    }

    private void PrepareTurn()
    {
        preparationRemaining = preparationSeconds;
        displayedSecond = -1;
        preparationPanel.SetActive(true);
        startTurnButton.interactable = true;
        RefreshTurnLabel();
        RefreshPreparationLabel();
        SetInput(true); // Permite organizar a equipe antes dos pedidos.
        TurnPreparing?.Invoke(session.TurnIndex, session.OrderCount);
    }

    public void StartTurn()
    {
        if (leaving || SceneLoader.IsTransitionInProgress || session == null || !session.StartTurn()) return;
        startTurnButton.interactable = false;
        preparationPanel.SetActive(false);
        TurnStarted?.Invoke(session.TurnIndex, session.OrderCount);
    }

    public bool TryResolveOrder(int turnIndex, int orderIndex, bool delivered)
    {
        if (!IsPlaying || !session.ResolveOrder(turnIndex, orderIndex, delivered)) return false;

        EventLogger.Instance.RecordActivityEvent(
            $"turn_{turnIndex + 1}_order_{orderIndex}_{(delivered ? "delivered" : "missed")}");
        RefreshTurnLabel();

        if (session.CurrentPhase == MinigameSessionState.Phase.Completed)
            FinishSession();
        return true;
    }

    private void FinishSession()
    {
        if (settled) return;
        settled = true;
        SetInput(false);
        preparationPanel.SetActive(false);
        backButton.interactable = false;
        int points = 0;
        if (!rewardGranted)
        {
            rewardGranted = true;
            points = PointsService.Instance.AwardParticipation();
            PlayerManager.Instance.SetPendingPoints(points, gameObject.scene.name);
        }

        EventLogger.Instance.RecordActivityEvent($"result_delivered_{session.Delivered}_missed_{session.Missed}");
        EventLogger.Instance.RecordActivityEvent($"participation_reward_{points}");
        EventLogger.Instance.CompleteSession();
        loggedSession = false;
        AudioManager.Instance?.PlayCompletion();
        resultPopup.Show(points, session.Delivered, session.Missed);
        SessionFinished?.Invoke();
    }

    public void BackToHub()
    {
        if (leaving || settled || SceneLoader.IsTransitionInProgress || sceneLoader == null) return;
        leaving = true;
        Abandon();
        SetInput(false);
        sceneLoader.Load("HUB");
    }

    private void Abandon()
    {
        session?.Abandon();
        if (!loggedSession) return;
        loggedSession = false;
        EventLogger.Instance?.AbandonSession();
    }

    private void RefreshTurnLabel()
    {
        if (turnLabel == null || session == null) return;
        turnLabel.SetText(
            "Turno {0}/{1} · {2}/{3}",
            session.TurnIndex + 1,
            session.TurnCount,
            session.ResolvedThisTurn,
            session.OrderCount);
    }

    private void RefreshPreparationLabel()
    {
        int seconds = Mathf.CeilToInt(preparationRemaining);
        if (seconds == displayedSecond) return;
        displayedSecond = seconds;
        preparationLabel.SetText("Organize-se: {0}", seconds);
    }

    private void SetInput(bool value)
    {
        if (gameplayInput == null) return;
        gameplayInput.interactable = value;
        gameplayInput.blocksRaycasts = value;
    }

    private bool SetupError(string detail)
    {
        Debug.LogError($"Sessão não iniciada — {name}: {detail}", this);
        return false;
    }

    private bool ValidateSetup()
    {
        if (preparationPanel == null) return SetupError("preparationPanel não atribuído.");
        if (preparationLabel == null) return SetupError("preparationLabel não atribuído.");
        if (turnLabel == null) return SetupError("turnLabel não atribuído.");
        if (startTurnButton == null) return SetupError("startTurnButton não atribuído.");
        if (backButton == null) return SetupError("backButton não atribuído.");
        if (gameplayInput == null) return SetupError("gameplayInput não atribuído.");
        if (resultPopup == null) return SetupError("resultPopup não atribuído.");
        if (sceneLoader == null) return SetupError("sceneLoader não atribuído.");
        if (string.IsNullOrWhiteSpace(activityId)) return SetupError("activityId vazio.");
        if (MaximumOrders < 1) return SetupError("ordersPerTurn precisa conter três inteiros positivos (4, 5, 6).");
        if (gameplayControllers != 1) return SetupError($"Controladores registrados: {gameplayControllers}; esperado: 1.");
        if (!gameplayConfigured) return SetupError("O controlador de gameplay falhou na validação. Consulte o erro específico no Console.");
        if (gameplayOrientation != ScreenOrientation.LandscapeLeft && gameplayOrientation != ScreenOrientation.LandscapeRight)
            return SetupError("gameplayOrientation precisa ser LandscapeLeft ou LandscapeRight.");
        if (EventLogger.Instance == null) return SetupError("EventLogger ausente. Execute pelo Boot.");
        if (PointsService.Instance == null) return SetupError("PointsService ausente. Execute pelo Boot.");
        if (PlayerManager.Instance == null) return SetupError("PlayerManager ausente. Execute pelo Boot.");
        if (PlayerManager.Instance.Profile == null) return SetupError("Perfil ausente. Conclua o cadastro pelo Boot.");
        return true;
    }

    private void OnDisable()
    {
        SceneLoader.SceneLeaving -= HandleSceneLeaving;
        StopAllCoroutines();
        leaving = true;
        Abandon();
        SetInput(false);
        RestoreOrientation();
    }

    private void RestoreOrientation()
    {
        if (orientationCaptured)
        {
            DeviceScreen.orientation = previousOrientation;
            orientationCaptured = false;
        }
    }

    private void OnDestroy()
    {
        if (startTurnButton != null) startTurnButton.onClick.RemoveListener(StartTurn);
        if (backButton != null) backButton.onClick.RemoveListener(BackToHub);
    }
}
