using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public sealed class LocalStorage : MonoBehaviour
{
    private const string EventsFileName = "events.json";
    private const string ParticipantKey = "MequiBreak.AnonymousParticipant";

    private string EventsPath => Path.Combine(Application.persistentDataPath, EventsFileName);
    private List<EventModel> cachedEvents;
    private bool eventsLoaded;
    private string anonymousParticipantId;

    public string GetAnonymousParticipantId()
    {
        if (!string.IsNullOrEmpty(anonymousParticipantId))
            return anonymousParticipantId;

        if (!PlayerPrefs.HasKey(ParticipantKey))
        {
            PlayerPrefs.SetString(ParticipantKey, Guid.NewGuid().ToString("N"));
            PlayerPrefs.Save();
        }

        anonymousParticipantId = PlayerPrefs.GetString(ParticipantKey);
        return anonymousParticipantId;
    }

    public void SaveEvents(List<EventModel> events)
    {
        if (events == null)
            return;

        File.WriteAllText(EventsPath, JsonUtility.ToJson(new EventListWrapper { Items = events }));
        cachedEvents = events;
        eventsLoaded = true;
    }

    public List<EventModel> LoadEvents()
    {
        if (eventsLoaded)
            return cachedEvents;

        eventsLoaded = true;

        if (!File.Exists(EventsPath))
        {
            cachedEvents = new List<EventModel>();
            return cachedEvents;
        }

        string json = File.ReadAllText(EventsPath);
        EventListWrapper wrapper = JsonUtility.FromJson<EventListWrapper>(json);
        cachedEvents = wrapper?.Items ?? new List<EventModel>();
        return cachedEvents;
    }

    [Serializable]
    private sealed class EventListWrapper
    {
        public List<EventModel> Items;
    }
}
