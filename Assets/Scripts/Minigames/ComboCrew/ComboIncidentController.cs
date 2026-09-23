using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ComboIncidentController : MonoBehaviour
{
    [Header("Incidentes")]
    [SerializeField] private bool incidentsEnabled;
    [SerializeField] private MinigameSessionController session;
    [SerializeField] private ComboCrewController controller;
    [Tooltip("Chance por turno: turno 1, 2 e 3.")]
    [SerializeField] private Vector3 chanceByTurn = new Vector3(0f, .35f, .5f);
    [SerializeField] private Vector2 triggerDelay = new Vector2(6f, 11f);
    [SerializeField, Min(1f)] private float absenceSeconds = 8f;
    [SerializeField, Min(1f)] private float repairSeconds = 5f;

    private Coroutine pending;
    private CrewMember absentMember;
    private KitchenStation brokenStation;
    private bool activeIncident;

    private void OnEnable()
    {
        if (session == null) return;
        session.TurnPreparing += PrepareTurn;
        session.TurnStarted += StartTurn;
        session.SessionFinished += ClearIncident;
    }

    private void OnDisable()
    {
        if (session != null)
        {
            session.TurnPreparing -= PrepareTurn;
            session.TurnStarted -= StartTurn;
            session.SessionFinished -= ClearIncident;
        }
        ClearIncident();
    }

    private void Update()
    {
        if (activeIncident && brokenStation != null && !brokenStation.IsBroken)
        {
            brokenStation = null;
            activeIncident = false;
        }
    }

    private void PrepareTurn(int index, int count)
    {
        ClearIncident();
    }

    private void StartTurn(int index, int count)
    {
        if (!incidentsEnabled || controller == null) return;
        float chance = index <= 0 ? chanceByTurn.x : index == 1 ? chanceByTurn.y : chanceByTurn.z;
        if (Random.value > Mathf.Clamp01(chance)) return;
        pending = StartCoroutine(Trigger(index));
    }

    private IEnumerator Trigger(int turn)
    {
        float delay = Random.Range(Mathf.Min(triggerDelay.x, triggerDelay.y), Mathf.Max(triggerDelay.x, triggerDelay.y));
        while (delay > 0f && session != null && session.IsPlaying)
        {
            delay -= Time.deltaTime;
            yield return null;
        }

        pending = null;
        if (session == null || !session.IsPlaying || activeIncident) yield break;

        bool started = Random.value < .5f ? TryAbsence() : TryBreakdown();
        if (!started) started = TryBreakdown() || TryAbsence();
        if (!started) yield break;

        activeIncident = true;
        if (absentMember == null) yield break;

        float remaining = absenceSeconds;
        while (remaining > 0f && session != null && session.IsPlaying)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        if (absentMember != null)
        {
            EventLogger.Instance?.RecordActivityEvent($"incident_absence_resolved_crew_{absentMember.MemberIndex}");
            absentMember.SetAbsent(false);
        }
        absentMember = null;
        activeIncident = false;
        controller?.ShowFeedback("Funcionário voltou para a equipe.");
    }

    private bool TryAbsence()
    {
        List<CrewMember> candidates = new List<CrewMember>();
        foreach (CrewMember member in controller.CrewMembers)
            if (member != null && member.CanBeAbsent && !member.IsAbsent) candidates.Add(member);
        if (candidates.Count == 0) return false;

        absentMember = candidates[Random.Range(0, candidates.Count)];
        absentMember.SetAbsent(true);
        controller.ShowFeedback("Imprevisto: um funcionário ficou indisponível por alguns segundos.", 2.4f);
        EventLogger.Instance?.RecordActivityEvent($"incident_absence_crew_{absentMember.MemberIndex}");
        AudioManager.Instance?.PlayIncident();
        MequiHaptics.Reject();
        return true;
    }

    private bool TryBreakdown()
    {
        List<KitchenStation> candidates = new List<KitchenStation>();
        foreach (KitchenStation station in controller.Stations)
            if (station != null && !station.IsBroken) candidates.Add(station);
        if (candidates.Count == 0) return false;

        brokenStation = candidates[Random.Range(0, candidates.Count)];
        if (!brokenStation.Break(repairSeconds))
        {
            brokenStation = null;
            return false;
        }

        controller.ShowFeedback($"Imprevisto: {brokenStation.ShortName} parou. Aloque alguém para reparar.", 2.6f);
        EventLogger.Instance?.RecordActivityEvent($"incident_breakdown_{brokenStation.Type.ToString().ToLowerInvariant()}");
        AudioManager.Instance?.PlayIncident();
        MequiHaptics.Reject();
        return true;
    }

    private void ClearIncident()
    {
        if (pending != null)
        {
            StopCoroutine(pending);
            pending = null;
        }
        if (absentMember != null) absentMember.SetAbsent(false);
        if (brokenStation != null) brokenStation.ForceRepair();
        absentMember = null;
        brokenStation = null;
        activeIncident = false;
    }
}
