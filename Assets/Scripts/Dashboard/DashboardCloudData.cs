using System;
using System.Collections.Generic;
using Firebase.Firestore;

public sealed class DashboardCloudData : IDisposable
{
    private static readonly string[] Activities = { "energy_station", "rush_balance", "combo_crew" };
    private const int ExpectedDocuments = 36;
    private readonly Dictionary<string, DashboardSessionData.Snapshot> values =
        new Dictionary<string, DashboardSessionData.Snapshot>();
    private readonly HashSet<string> received = new HashSet<string>();
    private readonly List<ListenerRegistration> listeners = new List<ListenerRegistration>();
    private readonly Action changed;

    public bool IsReady => received.Count == ExpectedDocuments;

    public DashboardCloudData(string store, DateTime currentMonth, Action onChanged)
    {
        changed = onChanged;
        FirebaseFirestore database = FirebaseSessionSync.Database;
        if (database == null || string.IsNullOrWhiteSpace(store)) return;

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
                    DocumentReference reference = database.Document(
                        "stores/" + storeKey + "/months/" + month +
                        "/shifts/" + shiftKey + "/games/" + activity);
                    listeners.Add(reference.Listen(snapshot => Receive(key, snapshot)));
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

    private void Receive(string key, DocumentSnapshot document)
    {
        values[key] = FromDocument(document);
        received.Add(key);
        if (IsReady) changed?.Invoke();
    }

    private static string Key(string month, int shift, string activity)
    {
        return month + "/" + FirebaseSessionSync.ShiftKey(shift) + "/" + activity;
    }

    private static DashboardSessionData.Snapshot FromDocument(DocumentSnapshot document)
    {
        var result = new DashboardSessionData.Snapshot();
        if (document == null || !document.Exists) return result;
        Dictionary<string, object> fields = document.ToDictionary();
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
        foreach (ListenerRegistration listener in listeners) listener.Stop();
        listeners.Clear();
        received.Clear();
        values.Clear();
    }
}