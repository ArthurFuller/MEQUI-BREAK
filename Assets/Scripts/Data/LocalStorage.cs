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

    public bool SaveEvents(List<EventModel> events)
    {
        if (events == null)
            return false;

        string pendingPath = EventsPath + ".tmp";
        try
        {
            File.WriteAllText(pendingPath, JsonUtility.ToJson(new EventListWrapper { Items = events }));
            if (File.Exists(EventsPath))
            {
                try
                {
                    File.Replace(pendingPath, EventsPath, EventsPath + ".bak");
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(EventsPath, EventsPath + ".bak", true);
                    File.Copy(pendingPath, EventsPath, true);
                    File.Delete(pendingPath);
                }
            }
            else
                File.Move(pendingPath, EventsPath);

            cachedEvents = new List<EventModel>(events);
            eventsLoaded = true;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public List<EventModel> LoadEvents()
    {
        if (eventsLoaded)
            return cachedEvents;

        eventsLoaded = true;
        if (TryReadEvents(EventsPath, out cachedEvents)
            || TryReadEvents(EventsPath + ".bak", out cachedEvents)
            || TryReadEvents(EventsPath + ".tmp", out cachedEvents))
            return cachedEvents;

        cachedEvents = new List<EventModel>();
        return cachedEvents;
    }

    private static bool TryReadEvents(string path, out List<EventModel> events)
    {
        events = null;
        if (!File.Exists(path))
            return false;

        try
        {
            events = JsonUtility.FromJson<EventListWrapper>(File.ReadAllText(path))?.Items;
            return events != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [Serializable]
    private sealed class EventListWrapper
    {
        public List<EventModel> Items;
    }
}
