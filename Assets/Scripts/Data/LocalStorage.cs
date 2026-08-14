using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public sealed class LocalStorage : MonoBehaviour
{
    private const string EventsFileName = "events.json";
    private const string ParticipantKey = "MequiBreak.AnonymousParticipant";

    private string EventsPath => Path.Combine(Application.persistentDataPath, EventsFileName);

    public string GetAnonymousParticipantId()
    {
        if (!PlayerPrefs.HasKey(ParticipantKey))
        {
            PlayerPrefs.SetString(ParticipantKey, Guid.NewGuid().ToString("N"));
            PlayerPrefs.Save();
        }

        return PlayerPrefs.GetString(ParticipantKey);
    }

    public void SaveEvents(List<EventModel> events)
    {
        if (events == null)
            return;

        File.WriteAllText(EventsPath, JsonUtility.ToJson(new EventListWrapper { Items = events }));
    }

    public List<EventModel> LoadEvents()
    {
        if (!File.Exists(EventsPath))
            return new List<EventModel>();

        string json = File.ReadAllText(EventsPath);
        EventListWrapper wrapper = JsonUtility.FromJson<EventListWrapper>(json);
        return wrapper?.Items ?? new List<EventModel>();
    }

    [Serializable]
    private sealed class EventListWrapper
    {
        public List<EventModel> Items;
    }
}
