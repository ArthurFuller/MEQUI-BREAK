using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EnergyStationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private EventLogger eventLogger;
    [SerializeField] private PointsService pointsService;

    [Header("UI")]
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private Button completeButton;

    [Header("Session")]
    [SerializeField, Min(1f)] private float maxDurationSeconds = 45f;
    [SerializeField, Min(1)] private int interactionsToComplete = 3;
    [SerializeField, Min(0.1f)] private float inactivityThresholdSeconds = 2f;
    [SerializeField] private string activityId = "energy_station";
    [SerializeField] private string resultScene = "Result";

    private float elapsedTime;
    private float lastInteractionTime;
    private bool gameplayActive;
    private bool sessionActive;
    private bool completeAvailable;
    private int interactionCount;

    private void Awake()
    {
        completeButton?.onClick.AddListener(CompleteSession);
        SetCompleteButton(false);
    }

    private void Start()
    {
        if (eventLogger == null)
            return;

        eventLogger.BeginSession(activityId);
        gameplayActive = true;
        sessionActive = true;
        elapsedTime = 0f;
        lastInteractionTime = 0f;
        interactionCount = 0;

        UpdateProgress();
    }

    private void Update()
    {
        if (!gameplayActive)
            return;

        elapsedTime += Time.unscaledDeltaTime;
        UpdateProgress();

        if (elapsedTime >= maxDurationSeconds)
            EndGameplayByTimeLimit();
    }

    private void OnDestroy()
    {
        completeButton?.onClick.RemoveListener(CompleteSession);
    }

    public void RegisterInteraction(string interactionId, string feedbackMessage)
    {
        if (!gameplayActive || eventLogger == null)
            return;

        float gap = interactionCount == 0 ? elapsedTime : elapsedTime - lastInteractionTime;
        if (gap >= inactivityThresholdSeconds)
            eventLogger.RecordInactive(gap);

        eventLogger.RecordUserAction(interactionId);
        interactionCount++;
        lastInteractionTime = elapsedTime;

        if (feedbackLabel != null)
            feedbackLabel.text = feedbackMessage ?? string.Empty;

        if (interactionCount >= interactionsToComplete)
            EndGameplayEarly();
    }

    public void RegisterOptionalClarity(string choiceId)
    {
        if (sessionActive)
            eventLogger?.RecordOptionalClarity(choiceId);
    }

    public void AbandonSession()
    {
        if (!sessionActive)
            return;

        sessionActive = false;
        gameplayActive = false;
        eventLogger?.AbandonSession();
    }

    private void EndGameplayEarly()
    {
        if (!gameplayActive)
            return;

        gameplayActive = false;
        completeAvailable = true;
        eventLogger?.MarkActivityCompletedEarly();
        eventLogger?.MarkCompleteButtonAvailable();
        SetCompleteButton(true);
    }

    private void EndGameplayByTimeLimit()
    {
        if (!gameplayActive)
            return;

        elapsedTime = maxDurationSeconds;
        gameplayActive = false;
        completeAvailable = true;
        eventLogger?.MarkTimeLimitReached();
        eventLogger?.MarkCompleteButtonAvailable();
        SetCompleteButton(true);
    }

    private void CompleteSession()
    {
        if (!sessionActive || !completeAvailable)
            return;

        sessionActive = false;
        eventLogger?.CompleteSession();
        pointsService?.AwardParticipation();
        sceneLoader?.Load(resultScene);
    }

    private void SetCompleteButton(bool enabled)
    {
        if (completeButton != null)
            completeButton.gameObject.SetActive(enabled);
    }

    private void UpdateProgress()
    {
        if (timerLabel != null)
        {
            float remaining = Mathf.Max(0f, maxDurationSeconds - elapsedTime);
            timerLabel.text = Mathf.CeilToInt(remaining).ToString();
        }

        if (progressBar != null)
            progressBar.value = maxDurationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsedTime / maxDurationSeconds);
    }
}
