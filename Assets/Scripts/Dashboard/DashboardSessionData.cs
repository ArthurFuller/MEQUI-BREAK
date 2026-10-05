using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class DashboardSessionData
{
    [Serializable] private sealed class EventFile { public List<EventModel> Items = new List<EventModel>(); }
    public sealed class Snapshot
    {
        public int Sessions, Completed, Delivered, Missed, ImpatientOrders, ImpatientMissed;
        public int Reorders, Assignments, Breaks, BreakReorganizations, StationProblems, StationRepairs;
        public int SpecialistPlacements, SpecialistOpportunities;
        public readonly int[] Choices = new int[4];
        public int TotalChoices => Choices[0] + Choices[1] + Choices[2] + Choices[3];
        public int TotalOrders => Delivered + Missed;
    }
    public static List<EventModel> Read(bool demonstration)
    {
        if (demonstration) return DemoSessions();
        LocalStorage storage = UnityEngine.Object.FindFirstObjectByType<LocalStorage>();
        if (storage != null) return new List<EventModel>(storage.LoadEvents());
        string path = Path.Combine(Application.persistentDataPath, "events.json");
        if (!File.Exists(path)) return new List<EventModel>();
        try { return JsonUtility.FromJson<EventFile>(File.ReadAllText(path))?.Items ?? new List<EventModel>(); }
        catch (Exception) { return new List<EventModel>(); }
    }
    public static Snapshot Aggregate(List<EventModel> sessions, string activity, DateTime month, int shift)
    {
        var result = new Snapshot();
        if (sessions == null) return result;
        string store = PlayerManager.Instance?.StoreId;
        foreach (EventModel session in sessions)
        {
            if (session == null || session.ActivityId != activity) continue;
            if (!string.IsNullOrWhiteSpace(store) && !string.Equals(session.StoreGroupId, store, StringComparison.OrdinalIgnoreCase)) continue;
            if (shift > 0 && ShiftIndex(session.Shift) != shift) continue;
            if (!DateTime.TryParse(session.StartedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime started)) continue;
            DateTime local = started.ToLocalTime();
            if (local.Year != month.Year || local.Month != month.Month) continue;
            AddSession(result, session);
        }
        return result;
    }
    public static Snapshot ForSession(EventModel session)
    {
        var result = new Snapshot();
        if (session != null) AddSession(result, session);
        return result;
    }
    private static void AddSession(Snapshot result, EventModel session)
    {
        result.Sessions++;
        if (session.SessionStatus == "Completed") result.Completed++;
        if (session.ActivityId == "energy_station") AddEnergy(result, session);
        else if (session.ActivityId == "rush_balance") AddRush(result, session);
        else if (session.ActivityId == "combo_crew") AddCombo(result, session);
    }
    public static int ShiftIndex(string shift)
    {
        if (string.Equals(shift, "Manhã", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(shift, "Tarde", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(shift, "Noite", StringComparison.OrdinalIgnoreCase)) return 3;
        return 0;
    }
    private static void AddEnergy(Snapshot result, EventModel session)
    {
        if (session.InteractionIds == null) return;
        foreach (string id in session.InteractionIds)
        {
            if (id == "energy_hydration") result.Choices[0]++;
            else if (id == "energy_break") result.Choices[1]++;
            else if (id == "energy_stretch") result.Choices[2]++;
            else if (id == "energy_restroom") result.Choices[3]++;
        }
    }
    private static void AddRush(Snapshot result, EventModel session)
    {
        if (session.InteractionIds != null)
            foreach (string id in session.InteractionIds) if (id == "rush_reorder") result.Reorders++;
        if (session.EventTypes == null) return;
        var impatientByOrder = new Dictionary<string, bool>();
        foreach (string entry in session.EventTypes)
        {
            if (entry == "rush_order_delivered") result.Delivered++;
            else if (entry == "rush_order_missed") result.Missed++;
            else if (entry != null && entry.StartsWith("rush_order_created:", StringComparison.Ordinal))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 4)
                {
                    bool impatient = parts[3] == "impatient";
                    impatientByOrder[parts[1]] = impatient;
                    if (impatient) result.ImpatientOrders++;
                }
            }
            else if (entry != null && entry.StartsWith("rush_order_resolved:", StringComparison.Ordinal))
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 3 && parts[2] == "missed" && impatientByOrder.TryGetValue(parts[1], out bool impatient) && impatient)
                    result.ImpatientMissed++;
            }
        }
    }
    private static void AddCombo(Snapshot result, EventModel session)
    {
        if (session.InteractionIds != null)
            foreach (string id in session.InteractionIds) if (id == "combo_assign_worker") result.Assignments++;
        if (session.EventTypes == null) return;
        foreach (string entry in session.EventTypes)
        {
            if (entry == "combo_order_delivered") result.Delivered++;
            else if (entry == "combo_order_missed") result.Missed++;
            else if (entry == "combo_worker_absent") result.Breaks++;
            else if (entry == "combo_break_reorganized") result.BreakReorganizations++;
            else if (entry == "combo_station_problem") result.StationProblems++;
            else if (entry == "combo_station_repaired") result.StationRepairs++;
            else if (entry != null && entry.StartsWith("combo_initial_specialists:", StringComparison.Ordinal))
            {
                string[] parts = entry.Split((char)58);
                if (parts.Length == 3 && int.TryParse(parts[1], out int placements) && int.TryParse(parts[2], out int opportunities))
                {
                    result.SpecialistPlacements += placements;
                    result.SpecialistOpportunities += opportunities;
                }
            }
        }
    }
    private static List<EventModel> DemoSessions()
    {
        // Fixed sample sessions exercise the same aggregation path as recorded events.
        // Each shift has a distinct distribution so the chart can be checked visually.
        var result = new List<EventModel>();
        string[] turns = { "Manhã", "Tarde", "Noite" };
        string[] energyIds = { "energy_hydration", "energy_break", "energy_stretch", "energy_restroom" };
        int[,,] energyCounts = {
            { { 8, 3, 2, 2 }, { 3, 7, 4, 1 }, { 2, 3, 4, 6 } },
            { { 5, 4, 3, 3 }, { 4, 4, 5, 2 }, { 3, 5, 4, 3 } }
        };
        int[,] rushMissed = { { 3, 7, 10 }, { 5, 8, 12 } };
        int[,] comboMissed = { { 2, 6, 9 }, { 4, 7, 11 } };
        int[,] specialistsPerSession = { { 3, 2, 1 }, { 2, 2, 1 } };
        DateTime now = DateTime.Now;
        string store = PlayerManager.Instance?.StoreId;

        for (int age = 0; age < 2; age++)
        {
            DateTime date = new DateTime(now.Year, now.Month, 1, 12, 0, 0).AddMonths(-age);
            for (int turn = 0; turn < 3; turn++)
            {
                var energyChoices = new List<string>(15);
                for (int category = 0; category < 4; category++)
                    for (int count = 0; count < energyCounts[age, turn, category]; count++)
                        energyChoices.Add(energyIds[category]);

                for (int play = 0; play < 5; play++)
                {
                    string started = date.AddHours(play).ToUniversalTime().ToString("O");
                    result.Add(new EventModel {
                        SessionId = "demo-energy-" + age + "-" + turn + "-" + play, ActivityId = "energy_station",
                        Shift = turns[turn], StoreGroupId = store, StartedAtUtc = started,
                        SessionStatus = "Completed",
                        InteractionIds = new List<string> {
                            energyChoices[play], energyChoices[play + 5], energyChoices[play + 10]
                        }
                    });

                    var rush = new EventModel {
                        SessionId = "demo-rush-" + age + "-" + turn + "-" + play, ActivityId = "rush_balance",
                        Shift = turns[turn], StoreGroupId = store, StartedAtUtc = started,
                        SessionStatus = "Completed", InteractionIds = new List<string>(),
                        EventTypes = new List<string>()
                    };
                    for (int order = 0; order < 4; order++)
                    {
                        int orderIndex = play * 4 + order;
                        bool impatient = order % 2 == 0;
                        bool missed = (orderIndex * 7 + turn * 3 + age * 5) % 20 < rushMissed[age, turn];
                        string id = order.ToString();
                        rush.EventTypes.Add("rush_order_created:" + id + ":" + (1 + order % 3) + ":" + (impatient ? "impatient" : "normal"));
                        rush.EventTypes.Add(missed ? "rush_order_missed" : "rush_order_delivered");
                        rush.EventTypes.Add("rush_order_resolved:" + id + ":" + (missed ? "missed" : "delivered"));
                    }
                    if (play < turn + 1) rush.InteractionIds.Add("rush_reorder");
                    result.Add(rush);

                    var combo = new EventModel {
                        SessionId = "demo-combo-" + age + "-" + turn + "-" + play, ActivityId = "combo_crew",
                        Shift = turns[turn], StoreGroupId = store, StartedAtUtc = started,
                        SessionStatus = "Completed",
                        InteractionIds = new List<string> { "combo_assign_worker" },
                        EventTypes = new List<string> { "combo_worker_absent", "combo_station_problem" }
                    };
                    combo.EventTypes.Add("combo_initial_specialists:" + specialistsPerSession[age, turn] + ":3");
                    if (play < Mathf.Max(0, 4 - turn - age))
                        combo.EventTypes.Add("combo_break_reorganized");
                    if (play < 3 - turn)
                        combo.EventTypes.Add("combo_station_repaired");
                    for (int order = 0; order < 4; order++)
                    {
                        int orderIndex = play * 4 + order;
                        bool missed = (orderIndex * 7 + turn * 3 + age * 5) % 20 < comboMissed[age, turn];
                        combo.EventTypes.Add(missed ? "combo_order_missed" : "combo_order_delivered");
                    }
                    result.Add(combo);
                }
            }
        }
        return result;
    }
}
