using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EventLogger : MonoBehaviour
{
    public static EventLogger Instance { get; private set; }

    [SerializeField] private LocalStorage localStorage;

    private readonly List<string> interactionIds = new List<string>();
    private readonly List<string> eventTypes = new List<string>();

    private string sessionId;
    private string activityId;
    private string startedAtUtc;
    private float sessionStartTime;
    private float gameplayEndTime;
    private float inactiveTime;
    private int interactionCount;
    private bool activityEnded;
    private bool completedEarly;
    private bool timeLimitReached;
    private string optionalClarityChoiceId;
    private int completeButtonAvailableCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void BeginSession(string id)
    {
        activityId = id;
        sessionId = Guid.NewGuid().ToString("N");
        startedAtUtc = DateTime.UtcNow.ToString("O");
        sessionStartTime = Time.realtimeSinceStartup;
        gameplayEndTime = 0f;
        inactiveTime = 0f;
        interactionCount = 0;
        activityEnded = false;
        completedEarly = false;
        timeLimitReached = false;
        optionalClarityChoiceId = null;
        completeButtonAvailableCount = 0;

        interactionIds.Clear();
        eventTypes.Clear();
        eventTypes.Add("SESSION_STARTED");
    }

    public void RecordUserAction(string interactionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        interactionCount++;
        interactionIds.Add(interactionId);
        eventTypes.Add("USER_ACTION");
    }

    public void RecordInactive(float seconds)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || seconds <= 0f)
            return;

        inactiveTime += seconds;
        eventTypes.Add("USER_INACTIVE");
    }

    public void RecordOptionalClarity(string choiceId)
    {
        optionalClarityChoiceId = choiceId;
    }

    public void MarkActivityCompletedEarly()
    {
        if (activityEnded)
            return;

        activityEnded = true;
        completedEarly = true;
        gameplayEndTime = Time.realtimeSinceStartup;
        eventTypes.Add("ACTIVITY_COMPLETED_EARLY");
    }

    public void MarkTimeLimitReached()
    {
        if (activityEnded)
            return;

        activityEnded = true;
        timeLimitReached = true;
        gameplayEndTime = Time.realtimeSinceStartup;
        eventTypes.Add("ACTIVITY_TIME_LIMIT_REACHED");
    }

    public void MarkCompleteButtonAvailable()
    {
        completeButtonAvailableCount++;
        eventTypes.Add("COMPLETE_BUTTON_AVAILABLE");
    }

    public void RecordAppBackground()
    {
        eventTypes.Add("APP_BACKGROUND");
        eventTypes.Add("SESSION_INTERRUPTED");
    }

    public void RecordAppForeground()
    {
        eventTypes.Add("APP_FOREGROUND");
    }

    public void CompleteSession()
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        float now = Time.realtimeSinceStartup;
        float activityDuration = activityEnded
            ? gameplayEndTime - sessionStartTime
            : now - sessionStartTime;
        float completionTime = now - sessionStartTime;
        float postActivityTime = activityEnded
            ? Mathf.Max(0f, now - gameplayEndTime)
            : 0f;
        float activeTime = Mathf.Max(0f, activityDuration - inactiveTime);

        eventTypes.Add("SESSION_COMPLETED");

        SaveSession(
            "Completed",
            activityDuration,
            completionTime,
            activeTime,
            inactiveTime,
            postActivityTime);
    }

    public void AbandonSession()
    {
        if (string.IsNullOrEmpty(sessionId))
            return;

        float now = Time.realtimeSinceStartup;
        float activityDuration = activityEnded
            ? gameplayEndTime - sessionStartTime
            : now - sessionStartTime;
        float completionTime = now - sessionStartTime;
        float activeTime = Mathf.Max(0f, activityDuration - inactiveTime);

        eventTypes.Add("SESSION_ABANDONED");

        SaveSession(
            "Abandoned",
            activityDuration,
            completionTime,
            activeTime,
            inactiveTime,
            0f);
    }

    private void SaveSession(
        string status,
        float activityDuration,
        float completionTime,
        float activeTime,
        float inactiveSeconds,
        float postActivityTime)
    {
        if (localStorage == null)
            return;

        List<EventModel> events = localStorage.LoadEvents();
        events.Add(new EventModel
        {
            SessionId = sessionId,
            AnonymousParticipantId = localStorage.GetAnonymousParticipantId(),
            ActivityId = activityId,
            StoreGroupId = PlayerManager.Instance?.Profile?.StoreId,
            Shift = PlayerManager.Instance?.Profile?.Shift,
            StartedAtUtc = startedAtUtc,
            ActivityDurationSeconds = Mathf.Max(0f, activityDuration),
            CompletionTimeSeconds = Mathf.Max(0f, completionTime),
            ActiveTimeSeconds = Mathf.Max(0f, activeTime),
            InactiveTimeSeconds = Mathf.Max(0f, inactiveSeconds),
            PostActivityTimeSeconds = Mathf.Max(0f, postActivityTime),
            InteractionCount = interactionCount,
            CompletedEarly = completedEarly,
            TimeLimitReached = timeLimitReached,
            SessionStatus = status,
            InteractionIds = new List<string>(interactionIds),
            EventTypes = new List<string>(eventTypes),
            OptionalClarityChoiceId = optionalClarityChoiceId
        });

        localStorage.SaveEvents(events);
        ResetSession();
    }

    private void ResetSession()
    {
        sessionId = null;
        activityId = null;
        startedAtUtc = null;
        interactionIds.Clear();
        eventTypes.Clear();
    }
}
