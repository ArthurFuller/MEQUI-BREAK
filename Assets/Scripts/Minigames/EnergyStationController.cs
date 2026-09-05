using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class EnergyStationController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private ResultPopup resultPopup;
    [SerializeField] private DraggableInteraction[] interactionCards;

    [Header("Interface")]
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private RectTransform progressBackground;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private Button completeButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TMP_Text instructionLabel;
    [SerializeField] private AvatarView avatarView;
    [SerializeField] private Image trayLockOverlay;
    [SerializeField] private CanvasGroup interactionTray;

    [Header("Estado do avatar")]
    [Tooltip("Índice da face de olhos fechados usada enquanto o Méqui descansa.")]
    [SerializeField, Range(0, 12)] private int sleepingFaceIndex;

    [Header("Mensagens")]
    [SerializeField, TextArea(2, 3)] private string initialMessage = "Que tipo de pausa o Méqui precisa hoje?";
    [SerializeField, TextArea(2, 3)] private string completedMessage = "Méqui está pronto para voltar ao trabalho!";
    [SerializeField, TextArea(2, 3)] private string timeExpiredMessage = "O tempo terminou. Toque em redefinir para tentar novamente.";

    [Header("Sessão")]
    [SerializeField, Min(1f)] private float maxDurationSeconds = 45f;
    [SerializeField, Min(1)] private int interactionsToComplete = 2;
    [SerializeField, Min(0.1f)] private float inactivityThresholdSeconds = 2f;
    [SerializeField] private string activityId = "energy_station";

    [Header("Animação do progresso")]
    [SerializeField, Min(0.05f)] private float progressAnimationDuration = 0.4f;

    private readonly HashSet<string> acceptedInteractionIds = new HashSet<string>();
    private float elapsedTime;
    private float lastInteractionTime;
    private SessionState sessionState;
    private int interactionCount;
    private Tween progressTween;
    private int lastDisplayedSecond = int.MinValue;
    private bool rewardGranted;
    private bool sessionStarted;

    public event System.Action<int> InteractionAccepted;

    public event System.Action ChoicesReset;

    public int InteractionsToComplete => interactionsToComplete;

    private enum SessionState
    {
        Playing,
        ReadyToComplete,
        TimeExpired,
        RewardCollected,
        Abandoned
    }

    private void Awake()
    {
        if (completeButton != null)
            completeButton.onClick.AddListener(CompleteSession);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetChoices);

        SetCompleteButton(false);
        SetInitialVisuals();
        SetTrayLocked(false);
        UpdateTimer();
        UpdateProgress(immediate: true);
    }

    private void Start()
    {
        PlayerManager player = PlayerManager.Instance;
        if (player != null && !player.CanPlayEnergyStation)
        {
            Debug.LogWarning("Energy Station indisponível: a atividade já foi concluída hoje.", this);
            SceneManager.LoadScene("HUB");
            return;
        }

        elapsedTime = 0f;
        lastInteractionTime = 0f;
        interactionCount = 0;
        lastDisplayedSecond = int.MinValue;
        acceptedInteractionIds.Clear();
        sessionState = SessionState.Playing;
        rewardGranted = false;

        ResetCardsToOrigin();
        BeginLoggedSession();

        SetInitialVisuals();
        SetTrayLocked(false);
        UpdateTimer();
        UpdateProgress(immediate: true);
    }

    private void Update()
    {
        if (sessionState != SessionState.Playing)
            return;

        elapsedTime += Time.unscaledDeltaTime;
        UpdateTimer();

        if (elapsedTime >= maxDurationSeconds)
            HandleTimeExpired();
    }

    private void OnDestroy()
    {
        if (completeButton != null)
            completeButton.onClick.RemoveListener(CompleteSession);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetChoices);

        if (progressTween != null && progressTween.IsActive())
            progressTween.Kill();
    }

    private void OnDisable()
    {
        if (progressTween != null && progressTween.IsActive())
            progressTween.Kill();

        progressTween = null;

        if (sessionStarted
            && sessionState != SessionState.RewardCollected
            && sessionState != SessionState.Abandoned)
        {
            sessionState = SessionState.Abandoned;
            sessionStarted = false;
            EventLogger.Instance?.AbandonSession();
        }
    }

    public bool TryRegisterInteraction(string interactionId, string feedbackMessage)
    {
        if (sessionState != SessionState.Playing ||
            interactionCount >= interactionsToComplete)
            return false;

        if (!string.IsNullOrEmpty(interactionId) &&
            !acceptedInteractionIds.Add(interactionId))
            return false;

        float gap = interactionCount == 0
            ? elapsedTime
            : elapsedTime - lastInteractionTime;

        if (gap >= inactivityThresholdSeconds)
            EventLogger.Instance?.RecordInactive(gap);

        EventLogger.Instance?.RecordUserAction(interactionId);

        interactionCount++;
        lastInteractionTime = elapsedTime;

        if (feedbackLabel != null)
            feedbackLabel.text = feedbackMessage ?? string.Empty;

        if (interactionCount >= interactionsToComplete)
            MakeCompletionAvailable();
        else
            UpdateProgress();

        InteractionAccepted?.Invoke(interactionCount);

        return true;
    }

    public void RegisterInteraction(string interactionId, string feedbackMessage)
    {
        TryRegisterInteraction(interactionId, feedbackMessage);
    }

    public void RegisterOptionalClarity(string choiceId)
    {
        if (sessionState == SessionState.RewardCollected ||
            sessionState == SessionState.Abandoned)
            return;

        EventLogger.Instance?.RecordOptionalClarity(choiceId);
    }

    public void AbandonSession()
    {
        if (sessionState == SessionState.RewardCollected ||
            sessionState == SessionState.Abandoned)
            return;

        sessionState = SessionState.Abandoned;
        sessionStarted = false;
        EventLogger.Instance?.AbandonSession();
    }

    public void ResetChoices()
    {
        if (sessionState == SessionState.RewardCollected ||
            sessionState == SessionState.Abandoned)
            return;

        bool restartExpiredAttempt = sessionState == SessionState.TimeExpired;
        if (restartExpiredAttempt)
        {
            if (sessionStarted)
                EventLogger.Instance?.AbandonSession();

            elapsedTime = 0f;
            lastDisplayedSecond = int.MinValue;
            rewardGranted = false;
            BeginLoggedSession();
        }

        sessionState = SessionState.Playing;
        interactionCount = 0;
        lastInteractionTime = elapsedTime;
        acceptedInteractionIds.Clear();

        SetCompleteButton(false);
        SetInitialVisuals();
        SetTrayLocked(false);
        ResetCardsToOrigin();
        UpdateTimer();
        UpdateProgress(immediate: restartExpiredAttempt);
        ChoicesReset?.Invoke();
    }

    public DraggableInteraction GetFirstAvailableInteraction()
    {
        if (interactionCards == null)
            return null;

        for (int i = 0; i < interactionCards.Length; i++)
        {
            DraggableInteraction interaction = interactionCards[i];
            if (interaction != null && interaction.IsAvailable)
                return interaction;
        }

        return null;
    }

    private void MakeCompletionAvailable()
    {
        if (sessionState != SessionState.Playing
            || interactionCount < interactionsToComplete)
            return;

        sessionState = SessionState.ReadyToComplete;
        EventLogger.Instance?.MarkActivityCompletedEarly();
        SetCompletedVisuals();
        SetTrayLocked(true);
        UpdateProgress();
        EventLogger.Instance?.MarkCompleteButtonAvailable();
        SetCompleteButton(true);
    }

    private void HandleTimeExpired()
    {
        if (sessionState != SessionState.Playing)
            return;

        sessionState = SessionState.TimeExpired;
        elapsedTime = maxDurationSeconds;
        EventLogger.Instance?.MarkTimeLimitReached();
        SetCompleteButton(false);
        SetTrayLocked(true);
        UpdateTimer();

        if (instructionLabel != null)
            instructionLabel.text = timeExpiredMessage;

        if (feedbackLabel != null)
            feedbackLabel.text = string.Empty;

        if (resetButton != null)
            resetButton.gameObject.SetActive(true);
    }

    private void CompleteSession()
    {
        if (sessionState != SessionState.ReadyToComplete
            || interactionCount < interactionsToComplete
            || rewardGranted)
            return;

        rewardGranted = true;
        sessionState = SessionState.RewardCollected;
        sessionStarted = false;
        SetCompleteButton(false);

        if (resetButton != null)
            resetButton.gameObject.SetActive(false);

        EventLogger.Instance?.CompleteSession();

        int pointsEarned = 0;
        if (PointsService.Instance != null)
        {
            pointsEarned = PointsService.Instance.AwardParticipation();
        }
        else
        {
            Debug.LogError(
                "EnergyStationController: PointsService.Instance não foi encontrado. " +
                "Verifique se o PointsService foi inicializado pelo Boot."
            );
        }

        // Os PB ficam pendentes até a animação de entrada do HUB.
        if (PlayerManager.Instance != null && pointsEarned > 0)
        {
            PlayerManager.Instance.SetPendingPoints(pointsEarned);
            PlayerManager.Instance.MarkEnergyStationCompleted();
        }

        AudioManager.Instance?.PlayCompletion();

        if (resultPopup != null)
        {
            resultPopup.Show(pointsEarned);
        }
        else
        {
            Debug.LogError(
                "EnergyStationController: ResultPopup não está configurado no Inspector."
            );
        }
    }

    private void SetInitialVisuals()
    {
        if (instructionLabel != null)
            instructionLabel.text = initialMessage;

        if (feedbackLabel != null)
            feedbackLabel.text = string.Empty;

        ApplySleepingFace();

        if (resetButton != null)
            resetButton.gameObject.SetActive(true);
    }

    private void BeginLoggedSession()
    {
        if (EventLogger.Instance == null)
        {
            sessionStarted = false;
            Debug.LogError(
                "EnergyStationController: EventLogger.Instance não foi encontrado. " +
                "Verifique se o EventLogger foi inicializado pelo Boot."
            );
            return;
        }

        EventLogger.Instance.BeginSession(activityId);
        sessionStarted = true;
    }

    private void SetCompletedVisuals()
    {
        if (instructionLabel != null)
            instructionLabel.text = completedMessage;

        ApplySelectedFace();
    }

    private void ApplySleepingFace()
    {
        AvatarCustomizationData customization = PlayerManager.Instance?.Profile?.Avatar;
        if (avatarView == null || customization == null)
            return;

        avatarView.Apply(customization);
        avatarView.ApplyFace(sleepingFaceIndex);
    }

    private void ApplySelectedFace()
    {
        AvatarCustomizationData customization = PlayerManager.Instance?.Profile?.Avatar;
        if (avatarView == null || customization == null)
            return;

        avatarView.ApplyFace(customization.FaceIndex);
    }

    private void SetTrayLocked(bool locked)
    {
        if (interactionTray != null)
        {
            interactionTray.interactable = !locked;
            interactionTray.blocksRaycasts = !locked;
        }

        if (locked)
            LockCardsForSessionEnd();

        // Referência mantida para cenas antigas.
        if (trayLockOverlay != null)
            trayLockOverlay.gameObject.SetActive(false);
    }

    private void LockCardsForSessionEnd()
    {
        if (interactionCards == null)
            return;

        for (int i = 0; i < interactionCards.Length; i++)
            interactionCards[i]?.LockForSessionEnd();
    }

    private void ResetCardsToOrigin()
    {
        if (interactionCards == null)
            return;

        for (int i = 0; i < interactionCards.Length; i++)
        {
            if (interactionCards[i] != null)
                interactionCards[i].ResetToOrigin();
        }
    }

    private void SetCompleteButton(bool enabled)
    {
        if (completeButton != null)
        {
            completeButton.gameObject.SetActive(enabled);
            completeButton.interactable = enabled;
        }
    }

    private void UpdateTimer()
    {
        if (timerLabel == null)
            return;

        float remaining = Mathf.Max(0f, maxDurationSeconds - elapsedTime);
        int displayedSecond = Mathf.CeilToInt(remaining);
        if (displayedSecond == lastDisplayedSecond)
            return;

        lastDisplayedSecond = displayedSecond;
        timerLabel.SetText("{0}", displayedSecond);
    }

    private void UpdateProgress(bool immediate = false)
    {
        if (progressBackground == null || progressFill == null || interactionsToComplete <= 0)
            return;

        float progress = Mathf.Clamp01((float)interactionCount / interactionsToComplete);
        float targetWidth = progressBackground.rect.width * progress;

        if (progressTween != null && progressTween.IsActive())
            progressTween.Kill();

        if (immediate || progressAnimationDuration <= 0f)
        {
            Vector2 sizeDelta = progressFill.sizeDelta;
            sizeDelta.x = targetWidth;
            progressFill.sizeDelta = sizeDelta;
            return;
        }

        Vector2 targetSize = progressFill.sizeDelta;
        targetSize.x = targetWidth;
        progressTween = progressFill
            .DOSizeDelta(targetSize, progressAnimationDuration)
            .SetEase(Ease.OutCubic);
    }
}
