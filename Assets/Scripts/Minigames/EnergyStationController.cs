using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnergyStationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ResultPopup resultPopup;

    [Header("UI")]
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private RectTransform progressBackground;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private Button completeButton;

    [Header("Session")]
    [SerializeField, Min(1f)] private float maxDurationSeconds = 45f;
    [SerializeField, Min(1)] private int interactionsToComplete = 3;
    [SerializeField, Min(0.1f)] private float inactivityThresholdSeconds = 2f;
    [SerializeField] private string activityId = "energy_station";

    private float elapsedTime;
    private float lastInteractionTime;
    private bool gameplayActive;
    private bool sessionActive;
    private bool completeAvailable;
    private int interactionCount;

    private void Awake()
    {
        if (completeButton != null)
            completeButton.onClick.AddListener(CompleteSession);

        SetCompleteButton(false);
        UpdateTimer();
        UpdateProgress();
    }

    private void Start()
    {
        elapsedTime = 0f;
        lastInteractionTime = 0f;
        interactionCount = 0;

        gameplayActive = true;
        sessionActive = true;
        completeAvailable = false;

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
        if (!gameplayActive)
            return;

        elapsedTime += Time.unscaledDeltaTime;

        UpdateTimer();

        if (elapsedTime >= maxDurationSeconds)
            EndGameplayByTimeLimit();
    }

    private void OnDestroy()
    {
        if (completeButton != null)
            completeButton.onClick.RemoveListener(CompleteSession);
    }

    public void RegisterInteraction(string interactionId, string feedbackMessage)
    {
        if (!gameplayActive)
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
            EndGameplayEarly();
    }

    public void RegisterOptionalClarity(string choiceId)
    {
        if (!sessionActive)
            return;

        EventLogger.Instance?.RecordOptionalClarity(choiceId);
    }

    public void AbandonSession()
    {
        if (!sessionActive)
            return;

        sessionActive = false;
        gameplayActive = false;

        EventLogger.Instance?.AbandonSession();
    }

    private void EndGameplayEarly()
    {
        if (!gameplayActive)
            return;

        gameplayActive = false;
        completeAvailable = true;

        EventLogger.Instance?.MarkActivityCompletedEarly();
        EventLogger.Instance?.MarkCompleteButtonAvailable();

        SetCompleteButton(true);
        UpdateProgress();
    }

    private void EndGameplayByTimeLimit()
    {
        if (!gameplayActive)
            return;

        elapsedTime = maxDurationSeconds;
        gameplayActive = false;
        completeAvailable = true;

        EventLogger.Instance?.MarkTimeLimitReached();
        EventLogger.Instance?.MarkCompleteButtonAvailable();

        SetCompleteButton(true);
        UpdateTimer();
    }

    private void CompleteSession()
    {
        if (!sessionActive || !completeAvailable)
            return;

        sessionActive = false;
        completeAvailable = false;

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

        // Set pending points for animation when entering Hub
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

        timerLabel.text = Mathf.CeilToInt(remaining).ToString();
    }

    private void UpdateProgress()
    {
        if (progressBackground == null || progressFill == null)
            return;

        if (interactionsToComplete <= 0)
            return;

        float progress = Mathf.Clamp01(
            (float)interactionCount / interactionsToComplete);

        float targetWidth = progressBackground.rect.width * progress;

        Vector2 sizeDelta = progressFill.sizeDelta;
        sizeDelta.x = targetWidth;
        progressFill.sizeDelta = sizeDelta;
    }
}