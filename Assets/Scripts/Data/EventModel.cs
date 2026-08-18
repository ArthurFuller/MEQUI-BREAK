using System;
using System.Collections.Generic;

[Serializable]
public sealed class EventModel
{
    public string SessionId;
    public string AnonymousParticipantId;
    public string ActivityId;
    public string StoreGroupId;
    public string Shift;
    public string StartedAtUtc;

    public float ActivityDurationSeconds;
    public float CompletionTimeSeconds;
    public float ActiveTimeSeconds;
    public float InactiveTimeSeconds;
    public float PostActivityTimeSeconds;

    public int InteractionCount;
    public bool CompletedEarly;
    public bool TimeLimitReached;
    public string SessionStatus;

    public List<string> InteractionIds = new List<string>();
    public List<string> EventTypes = new List<string>();
    public string OptionalClarityChoiceId;
}
