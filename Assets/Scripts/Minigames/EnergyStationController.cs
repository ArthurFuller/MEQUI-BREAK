using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnergyStationController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private ResultPopup resultPopup;

    [Header("UI")]
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private RectTransform progressBackground;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private Button completeButton;

    [Header("Sessão")]
    [SerializeField, Min(1f)] private float maxDurationSeconds = 45f;
    [SerializeField, Min(1)] private int interactionsToComplete = 3;
    [SerializeField, Min(0.1f)] private float inactivityThresholdSeconds = 2f;
    [SerializeField] private string activityId = "energy_station";

    [Header("Animação do progresso")]
    [SerializeField, Min(0.05f)] private float progressAnimationDuration = 0.4f;

    private float elapsedTime;
    private float lastInteractionTime;
    private SessionState sessionState;
    private int interactionCount;
    private Tween _progressTween;
    private int _lastDisplayedSecond = int.MinValue;

    private enum SessionState
    {
        Playing,
        ReadyToComplete,
        Finished,
        Abandoned
    }

    private void Awake()
    {
        if (completeButton != null)
            completeButton.onClick.AddListener(CompleteSession);

        SetCompleteButton(false);
        UpdateTimer();
        UpdateProgress(immediate: true);
    }

    private void Start()
    {
        elapsedTime = 0f;
        lastInteractionTime = 0f;
        interactionCount = 0;

        sessionState = SessionState.Playing;

        if (EventLogger.Instance != null)
        {
            EventLogger.Instance.BeginSession(activityId);
        }
        else
        {
            Debug.LogError(
                "EnergyStationController: EventLogger.Instance não foi encontrado. " +
                "Verifique se o EventLogger foi inicializado pelo Boot."
            );
        }

        UpdateTimer();
        UpdateProgress();
    }

    private void Update()
    {
        if (sessionState != SessionState.Playing)
            return;

        elapsedTime += Time.unscaledDeltaTime;

        UpdateTimer();

        if (elapsedTime >= maxDurationSeconds)
            MakeCompletionAvailable(completedEarly: false);
    }

    private void OnDestroy()
    {
        if (completeButton != null)
            completeButton.onClick.RemoveListener(CompleteSession);

        if (_progressTween != null && _progressTween.IsActive())
            _progressTween.Kill();
    }

    public void RegisterInteraction(string interactionId, string feedbackMessage)
    {
        if (sessionState != SessionState.Playing)
            return;

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

        UpdateProgress();

        if (interactionCount >= interactionsToComplete)
            MakeCompletionAvailable(completedEarly: true);
    }

    public void RegisterOptionalClarity(string choiceId)
    {
        if (sessionState == SessionState.Finished ||
            sessionState == SessionState.Abandoned)
            return;

        EventLogger.Instance?.RecordOptionalClarity(choiceId);
    }

    public void AbandonSession()
    {
        if (sessionState == SessionState.Finished ||
            sessionState == SessionState.Abandoned)
            return;

        sessionState = SessionState.Abandoned;

        EventLogger.Instance?.AbandonSession();
    }

    private void MakeCompletionAvailable(bool completedEarly)
    {
        if (sessionState != SessionState.Playing)
            return;

        sessionState = SessionState.ReadyToComplete;

        if (completedEarly)
        {
            EventLogger.Instance?.MarkActivityCompletedEarly();
            UpdateProgress();
        }
        else
        {
            elapsedTime = maxDurationSeconds;
            EventLogger.Instance?.MarkTimeLimitReached();
            UpdateTimer();
        }

        EventLogger.Instance?.MarkCompleteButtonAvailable();
        SetCompleteButton(true);
    }

    private void CompleteSession()
    {
        if (sessionState != SessionState.ReadyToComplete)
            return;

        sessionState = SessionState.Finished;

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

        // Mantém os pontos pendentes para a animação ao entrar no HUB.
        if (PlayerManager.Instance != null && pointsEarned > 0)
        {
            PlayerManager.Instance.SetPendingPoints(pointsEarned);
        }

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

    private void SetCompleteButton(bool enabled)
    {
        if (completeButton != null)
            completeButton.gameObject.SetActive(enabled);
    }

    private void UpdateTimer()
    {
        if (timerLabel == null)
            return;

        float remaining = Mathf.Max(
            0f,
            maxDurationSeconds - elapsedTime);

        int displayedSecond = Mathf.CeilToInt(remaining);
        if (displayedSecond == _lastDisplayedSecond)
            return;

        _lastDisplayedSecond = displayedSecond;
        timerLabel.SetText("{0}", displayedSecond);
    }

    private void UpdateProgress(bool immediate = false)
    {
        if (progressBackground == null || progressFill == null)
            return;

        if (interactionsToComplete <= 0)
            return;

        float progress = Mathf.Clamp01(
            (float)interactionCount / interactionsToComplete);

        float targetWidth = progressBackground.rect.width * progress;

        if (immediate || progressAnimationDuration <= 0f)
        {
            // Atualização instantânea durante a inicialização ou sem animação.
            Vector2 sizeDelta = progressFill.sizeDelta;
            sizeDelta.x = targetWidth;
            progressFill.sizeDelta = sizeDelta;

            // Encerra qualquer tween anterior.
            if (_progressTween != null && _progressTween.IsActive())
                _progressTween.Kill();
        }
        else
        {
            // Anima com OutCubic para uma finalização suave.
            if (_progressTween != null && _progressTween.IsActive())
                _progressTween.Kill();

            Vector2 targetSize = progressFill.sizeDelta;
            targetSize.x = targetWidth;
            _progressTween = progressFill
                .DOSizeDelta(targetSize, progressAnimationDuration)
                .SetEase(Ease.OutCubic);
        }
    }
}
