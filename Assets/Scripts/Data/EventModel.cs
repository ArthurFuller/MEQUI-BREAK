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
    public float DurationSeconds;
    public float AverageResponseLatencySeconds;
    public bool Completed;
    public bool Abandoned;
    public List<string> ChoiceIds = new List<string>();
    public string OptionalClarityChoiceId;
}
