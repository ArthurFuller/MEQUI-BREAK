using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FirebaseSessionSync : MonoBehaviour
{
    [Serializable] private sealed class SyncState { public List<string> SessionIds = new List<string>(); }

    private const string SyncFileName = "firebase-synced.json";
    private const float RetrySeconds = 20f;
    private readonly HashSet<string> synced = new HashSet<string>();
    private FirebaseFirestore database;
    private FirebaseAuth auth;
    private bool syncRequested = true;

    public static FirebaseSessionSync Instance { get; private set; }
    public static event Action ReadyChanged;
    public static FirebaseFirestore Database => Instance != null ? Instance.database : null;
    public static string LastError { get; private set; }

    private string SyncPath => Path.Combine(Application.persistentDataPath, SyncFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        try
        {
            if (File.Exists(SyncPath))
            {
                SyncState state = JsonUtility.FromJson<SyncState>(File.ReadAllText(SyncPath));
                if (state?.SessionIds != null)
                    foreach (string id in state.SessionIds) synced.Add(id);
            }
        }
        catch (Exception)
        {
            // A lost receipt only causes an idempotent retry of the same session ID.
        }
    }

    private IEnumerator Start()
    {
        while (isActiveAndEnabled)
        {
            if (database == null)
                yield return Initialize();
            if (database != null)
                yield return UploadPending();

            float deadline = Time.realtimeSinceStartup + RetrySeconds;
            while (!syncRequested && Time.realtimeSinceStartup < deadline)
                yield return null;
            syncRequested = false;
        }
    }

    public static void RequestSync()
    {
        if (Instance != null) Instance.syncRequested = true;
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) RequestSync();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        ReadyChanged = null;
    }

    private IEnumerator Initialize()
    {
        var dependencies = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencies.IsCompleted);
        if (dependencies.IsFaulted || dependencies.IsCanceled ||
            dependencies.Result != DependencyStatus.Available)
        {
            LastError = "Firebase indisponível";
            yield break;
        }

        auth = FirebaseAuth.DefaultInstance;
        var signIn = auth.SignInAnonymouslyAsync();
        yield return new WaitUntil(() => signIn.IsCompleted);
        if (signIn.IsFaulted || signIn.IsCanceled)
        {
            LastError = "Autenticação anônima indisponível";
            yield break;
        }

        database = FirebaseFirestore.DefaultInstance;
        LastError = null;
        ReadyChanged?.Invoke();
    }

    private IEnumerator UploadPending()
    {
        LocalStorage storage = FindFirstObjectByType<LocalStorage>();
        if (storage == null || auth?.CurrentUser == null) yield break;

        foreach (EventModel session in new List<EventModel>(storage.LoadEvents()))
        {
            if (session == null || synced.Contains(session.SessionId) || !CanUpload(session))
                continue;

            Dictionary<string, object> document = BuildDocument(session, auth.CurrentUser.UserId);
            var upload = UploadSessionAsync(document, session.SessionId);
            yield return new WaitUntil(() => upload.IsCompleted);
            if (upload.IsFaulted || upload.IsCanceled)
            {
                LastError = "Sincronização pendente";
                yield break;
            }

            synced.Add(session.SessionId);
            SaveSyncState();
            LastError = null;
        }
    }

    private Task UploadSessionAsync(Dictionary<string, object> document, string sessionId)
    {
        DocumentReference sessionRef = database.Collection("sessions").Document(sessionId);
        string basePath = "stores/" + document["storeKey"] + "/months/" +
            document["monthKey"] + "/shifts/";
        DocumentReference allRef = database.Document(
            basePath + "all/games/" + document["activity"]);
        DocumentReference shiftRef = database.Document(
            basePath + document["shiftKey"] + "/games/" + document["activity"]);
        var metrics = (Dictionary<string, object>)document["metrics"];

        return database.RunTransactionAsync(async transaction =>
        {
            DocumentSnapshot existing = await transaction.GetSnapshotAsync(sessionRef);
            if (existing.Exists) return;

            DocumentSnapshot all = await transaction.GetSnapshotAsync(allRef);
            DocumentSnapshot shift = await transaction.GetSnapshotAsync(shiftRef);
            transaction.Set(sessionRef, document, SetOptions.Overwrite);
            transaction.Set(allRef, AddMetrics(all, metrics, sessionId), SetOptions.Overwrite);
            transaction.Set(shiftRef, AddMetrics(shift, metrics, sessionId), SetOptions.Overwrite);
        });
    }

    private static Dictionary<string, object> AddMetrics(
        DocumentSnapshot previous, Dictionary<string, object> metrics, string sessionId)
    {
        Dictionary<string, object> old = previous.Exists
            ? previous.ToDictionary() : new Dictionary<string, object>();
        var updated = new Dictionary<string, object>();
        foreach (KeyValuePair<string, object> metric in metrics)
        {
            long before = old.TryGetValue(metric.Key, out object value) ? Convert.ToInt64(value) : 0L;
            updated[metric.Key] = checked(before + Convert.ToInt64(metric.Value));
        }
        updated["lastSessionId"] = sessionId;
        return updated;
    }
    private void SaveSyncState()
    {
        try
        {
            File.WriteAllText(SyncPath, JsonUtility.ToJson(new SyncState { SessionIds = new List<string>(synced) }));
        }
        catch (Exception)
        {
            // A later upload of this same document cannot create another session.
        }
    }

    private static bool CanUpload(EventModel session)
    {
        return Guid.TryParse(session.SessionId, out _) &&
            !string.IsNullOrWhiteSpace(session.AnonymousParticipantId) &&
            !string.IsNullOrWhiteSpace(session.StoreGroupId) &&
            DashboardSessionData.ShiftIndex(session.Shift) > 0 &&
            (session.ActivityId == "energy_station" ||
             session.ActivityId == "rush_balance" ||
             session.ActivityId == "combo_crew") &&
            DateTime.TryParse(session.StartedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _);
    }

    private static Dictionary<string, object> BuildDocument(EventModel session, string ownerUid)
    {
        DateTime started = DateTime.Parse(session.StartedAtUtc, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind).ToLocalTime();
        DashboardSessionData.Snapshot counts = DashboardSessionData.ForSession(session);
        return new Dictionary<string, object>
        {
            ["ownerUid"] = ownerUid,
            ["deviceId"] = session.AnonymousParticipantId,
            ["sessionId"] = session.SessionId,
            ["storeKey"] = StoreKey(session.StoreGroupId),
            ["monthKey"] = MonthKey(started),
            ["shiftKey"] = ShiftKey(DashboardSessionData.ShiftIndex(session.Shift)),
            ["activity"] = session.ActivityId,
            ["startedAtUtc"] = session.StartedAtUtc,
            ["metrics"] = new Dictionary<string, object>
            {
                ["sessions"] = counts.Sessions,
                ["completed"] = counts.Completed,
                ["delivered"] = counts.Delivered,
                ["missed"] = counts.Missed,
                ["impatientOrders"] = counts.ImpatientOrders,
                ["impatientMissed"] = counts.ImpatientMissed,
                ["reorders"] = counts.Reorders,
                ["assignments"] = counts.Assignments,
                ["breaks"] = counts.Breaks,
                ["breakReorganizations"] = counts.BreakReorganizations,
                ["stationProblems"] = counts.StationProblems,
                ["stationRepairs"] = counts.StationRepairs,
                ["specialistPlacements"] = counts.SpecialistPlacements,
                ["specialistOpportunities"] = counts.SpecialistOpportunities,
                ["choice0"] = counts.Choices[0],
                ["choice1"] = counts.Choices[1],
                ["choice2"] = counts.Choices[2],
                ["choice3"] = counts.Choices[3]
            }
        };
    }

    public static string StoreKey(string store)
    {
        byte[] normalized = Encoding.UTF8.GetBytes((store ?? "").Trim().ToUpperInvariant());
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", "").ToLowerInvariant();
    }

    public static string MonthKey(DateTime date) => date.ToString("yyyyMM", CultureInfo.InvariantCulture);

    public static string ShiftKey(int shift)
    {
        return shift == 1 ? "morning" : shift == 2 ? "afternoon" : shift == 3 ? "night" : "all";
    }
}