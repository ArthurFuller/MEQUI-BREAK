using System;
using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine;
#else
using Firebase.Firestore;
#endif

public sealed class DashboardCloudData : IDisposable
{
    private static readonly string[] Activities = { "energy_station", "rush_balance", "combo_crew" };
    private const int ExpectedDocuments = 36;
    private readonly Dictionary<string, DashboardSessionData.Snapshot> values =
        new Dictionary<string, DashboardSessionData.Snapshot>();
    private readonly HashSet<string> received = new HashSet<string>();
#if UNITY_WEBGL && !UNITY_EDITOR
    private readonly List<int> listeners = new List<int>();
    private static readonly string[] MetricNames = { "sessions", "completed", "delivered", "missed", "impatientOrders", "impatientMissed", "reorders", "assignments", "breaks", "breakReorganizations", "stationProblems", "stationRepairs", "specialistPlacements", "specialistOpportunities", "choice0", "choice1", "choice2", "choice3" };
#else
    private readonly List<ListenerRegistration> listeners = new List<ListenerRegistration>();
#endif
    private readonly Action changed;

    public bool IsReady => received.Count == ExpectedDocuments;

    public DashboardCloudData(string store, DateTime currentMonth, Action onChanged)
    {
        changed = onChanged;
        if (!FirebaseSessionSync.IsReady || string.IsNullOrWhiteSpace(store)) return;
#if !UNITY_WEBGL || UNITY_EDITOR
        FirebaseFirestore database = FirebaseSessionSync.Database;
#endif

        string storeKey = FirebaseSessionSync.StoreKey(store);
        for (int age = 0; age < 3; age++)
        {
            string month = FirebaseSessionSync.MonthKey(currentMonth.AddMonths(-age));
            for (int shift = 0; shift < 4; shift++)
            {
                string shiftKey = FirebaseSessionSync.ShiftKey(shift);
                foreach (string activity in Activities)
                {
                    string key = Key(month, shift, activity);
                    string documentPath = "stores/" + storeKey + "/months/" + month +
                        "/shifts/" + shiftKey + "/games/" + activity;
#if UNITY_WEBGL && !UNITY_EDITOR
                    listeners.Add(FirebaseWebBridge.Listen(documentPath, response =>
                    {
                        if (!string.IsNullOrEmpty(response.error))
                        {
                            received.Remove(key);
                            changed?.Invoke();
                            Debug.LogWarning("Firebase dashboard: " + response.error);
                            return;
                        }
                        var fields = new Dictionary<string, object>();
                        for (int i = 0; i < MetricNames.Length && i < (response.metrics?.Length ?? 0); i++)
                            fields[MetricNames[i]] = response.metrics[i];
                        Receive(key, fields);
                    }));
#else
                    listeners.Add(database.Document(documentPath).Listen(snapshot =>
                        Receive(key, snapshot.Exists ? snapshot.ToDictionary() : null)));
#endif
                }
            }
        }
    }

    public DashboardSessionData.Snapshot Get(string activity, DateTime month, int shift)
    {
        string key = Key(FirebaseSessionSync.MonthKey(month), shift, activity);
        return values.TryGetValue(key, out DashboardSessionData.Snapshot value)
            ? value : new DashboardSessionData.Snapshot();
    }

    private void Receive(string key, Dictionary<string, object> fields)
    {
        values[key] = FromFields(fields);
        received.Add(key);
        if (IsReady) changed?.Invoke();
    }

    private static string Key(string month, int shift, string activity)
    {
        return month + "/" + FirebaseSessionSync.ShiftKey(shift) + "/" + activity;
    }

    private static DashboardSessionData.Snapshot FromFields(Dictionary<string, object> fields)
    {
        var result = new DashboardSessionData.Snapshot();
        if (fields == null) return result;
        result.Sessions = Number(fields, "sessions");
        result.Completed = Number(fields, "completed");
        result.Delivered = Number(fields, "delivered");
        result.Missed = Number(fields, "missed");
        result.ImpatientOrders = Number(fields, "impatientOrders");
        result.ImpatientMissed = Number(fields, "impatientMissed");
        result.Reorders = Number(fields, "reorders");
        result.Assignments = Number(fields, "assignments");
        result.Breaks = Number(fields, "breaks");
        result.BreakReorganizations = Number(fields, "breakReorganizations");
        result.StationProblems = Number(fields, "stationProblems");
        result.StationRepairs = Number(fields, "stationRepairs");
        result.SpecialistPlacements = Number(fields, "specialistPlacements");
        result.SpecialistOpportunities = Number(fields, "specialistOpportunities");
        for (int i = 0; i < result.Choices.Length; i++)
            result.Choices[i] = Number(fields, "choice" + i);
        return result;
    }

    private static int Number(Dictionary<string, object> fields, string key)
    {
        if (fields == null || !fields.TryGetValue(key, out object value)) return 0;
        try { return (int)Math.Min(int.MaxValue, Math.Max(0L, Convert.ToInt64(value))); }
        catch (Exception) { return 0; }
    }

    public void Dispose()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        foreach (int listener in listeners) FirebaseWebBridge.Stop(listener);
#else
        foreach (ListenerRegistration listener in listeners) listener.Stop();
#endif
        listeners.Clear();
        received.Clear();
        values.Clear();
    }
}