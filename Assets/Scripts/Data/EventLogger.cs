using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EventLogger : MonoBehaviour
{
    public static EventLogger Instance { get; private set; }

    [SerializeField] private LocalStorage localStorage;

    private readonly List<string> choices = new List<string>();
    private string sessionId;
    private string activityId;
    private string startedAtUtc;
    private float sessionStartTime;
    private float totalResponseLatency;
    private int responseCount;
    private string optionalClarityChoiceId;

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
        totalResponseLatency = 0f;
        responseCount = 0;
        optionalClarityChoiceId = null;
        choices.Clear();
    }

    public void RecordChoice(string choiceId)
    {
        if (!string.IsNullOrWhiteSpace(choiceId))
            choices.Add(choiceId);
    }

    public void RecordResponseLatency(float seconds)
    {
        if (seconds >= 0f)
        {
            totalResponseLatency += seconds;
            responseCount++;
        }
    }

    public void RecordOptionalClarity(string choiceId)
    {
        optionalClarityChoiceId = choiceId;
    }

    public void CompleteSession() => SaveSession(true);
    public void AbandonSession() => SaveSession(false);

    private void SaveSession(bool completed)
    {
        if (string.IsNullOrEmpty(sessionId) || localStorage == null)
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
            DurationSeconds = Time.realtimeSinceStartup - sessionStartTime,
            AverageResponseLatencySeconds = responseCount > 0 ? totalResponseLatency / responseCount : 0f,
            Completed = completed,
            Abandoned = !completed,
            ChoiceIds = new List<string>(choices),
            OptionalClarityChoiceId = optionalClarityChoiceId
        });

        localStorage.SaveEvents(events);
        sessionId = null;
    }
}
