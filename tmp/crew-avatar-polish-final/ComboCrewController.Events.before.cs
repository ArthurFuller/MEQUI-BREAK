using System;
using UnityEngine;

public sealed partial class ComboCrewController
{
    private sealed class TurnEvent
    {
        public bool IsBreak;
        public float At, Duration;
        public bool Resolved;
    }

    private ComboCrewTurnProfile turnProfile;
    private System.Random turnRandom;
    private int turnSeed;
    private float turnElapsed;
    private TurnEvent[] turnEvents;
    private Incident[] stationIncidents;
    private float[] stationTimers, stationRepairDurations, stationReadyAt;
    private float[] workerWarnings, workerBreakDurations, workerReadyAt;
    private bool[] workerAway, workerAvailable, workerBreakPending;
    private bool crewFeasible;

    private void EnsureRoster()
    {
        ComboCrewWorkerProfile[] extra = Resources.LoadAll<ComboCrewWorkerProfile>("ComboCrew/Workers");
        if (extra.Length == 0)
        {
            Debug.LogError("Combo Crew: perfis de funcionários adicionais não encontrados em Resources/ComboCrew/Workers.", this);
            return;
        }
        Array.Sort(extra, (a, b) => string.CompareOrdinal(a.name, b.name));
        int original = workers.Length;
        Array.Resize(ref workers, original + extra.Length);
        Array.Resize(ref workerCards, original + extra.Length);
        Array.Resize(ref cardAnchors, original + extra.Length);
        for (int i = 0; i < extra.Length; i++)
        {
            int index = original + i;
            int template = 1 + i % (original - 1);
            ComboCrewWorkerView worker = Instantiate(workers[template], workers[template].transform.parent);
            worker.name = "Funcionario_" + index + "_Ator";
            worker.ApplyProfile(extra[i]);
            worker.GetComponent<ComboCrewWorkerStationDrag>()?.SetWorkerIndex(index);
            worker.gameObject.SetActive(false);
            RectTransform anchor = Instantiate(cardAnchors[template], cardAnchors[template].parent);
            anchor.name = "AncoraFuncionario_" + index;
            ComboCrewWorkerCard card = Instantiate(workerCards[template], workerCards[template].transform.parent);
            card.name = "CardFuncionario_" + index;
            card.SetWorkerIndex(index);
            workers[index] = worker;
            workerCards[index] = card;
            cardAnchors[index] = anchor;
        }
    }

    private void EnsureTurnProfiles()
    {
        string[] profilePaths =
        {
            "ComboCrew/ComboCrewTurnCalm",
            "ComboCrew/ComboCrewTurnBusy",
            "ComboCrew/ComboCrewTurnIntense"
        };
        if (turnProfiles == null || turnProfiles.Length != 3)
        {
            ComboCrewTurnProfile[] saved = turnProfiles;
            turnProfiles = new ComboCrewTurnProfile[3];
            if (saved != null)
                Array.Copy(saved, turnProfiles, Math.Min(saved.Length, turnProfiles.Length));
        }
        generatedProfiles = new bool[turnProfiles.Length];
        for (int i = 0; i < turnProfiles.Length; i++)
        {
            if (turnProfiles[i] != null) continue;
            turnProfiles[i] = Resources.Load<ComboCrewTurnProfile>(profilePaths[i]);
            if (turnProfiles[i] != null) continue;
            turnProfiles[i] = ComboCrewTurnProfile.CreateFallback(i);
            generatedProfiles[i] = true;
            Debug.LogWarning($"Combo Crew: perfil de turno {i} não carregou; usando valores padrão nesta partida.", this);
        }
    }

    private bool IsUnavailable(int worker) => worker < 0 || worker >= workers.Length
        || !workerAvailable[worker] || workerAway[worker];
    private bool IsWarned(int worker) => worker >= 0 && workerBreakPending[worker];
    private bool IsBlocked(int station) => stationIncidents[station] == Incident.Broken
        || stationIncidents[station] == Incident.Repairing;
    private bool AnyBreakActive()
    {
        for (int i = 0; i < workers.Length; i++)
            if (workerAway[i] || workerBreakPending[i]) return true;
        return false;
    }

    private int AvailableWorkerCount()
    {
        int count = 0;
        foreach (bool available in workerAvailable) if (available) count++;
        return count;
    }

    private bool HasRoomForDisruption()
    {
        int active = 0;
        foreach (Incident state in stationIncidents)
            if (state != Incident.None) active++;
        for (int i = 0; i < workers.Length; i++)
            if (workerAway[i] || workerBreakPending[i]) active++;
        return active < Mathf.Max(1, AvailableWorkerCount() - 3);
    }

    private bool CrewCanKeepPace(int station, bool[] occupied, float worstWorkSeconds)
    {
        if (station == MainStationCount) return true;
        for (int i = 0; i < workers.Length; i++)
        {
            if (!workerAvailable[i] || occupied[i]
                || workers[i].CycleSeconds(station, worstWorkSeconds) > spawnInterval) continue;
            occupied[i] = true;
            if (CrewCanKeepPace(station + 1, occupied, worstWorkSeconds)) return true;
            occupied[i] = false;
        }
        return false;
    }

    private bool CrewCanKeepPace()
    {
        if (AvailableWorkerCount() < MainStationCount) return false;
        Vector3 weights = turnProfile.orderSizeWeights;
        Vector3 work = turnProfile.orderTimeMultipliers;
        float largest = weights.x > 0f ? work.x : 0f;
        if (weights.y > 0f) largest = Mathf.Max(largest, work.y);
        if (weights.z > 0f) largest = Mathf.Max(largest, work.z);
        if (largest <= 0f) largest = work.x;
        return CrewCanKeepPace(0, new bool[workers.Length],
            stageSeconds * Mathf.Max(.1f, largest));
    }

    private int RollOrderSize()
    {
        Vector3 weights = turnProfile.orderSizeWeights;
        float one = Mathf.Max(0f, weights.x), two = Mathf.Max(0f, weights.y);
        float total = one + two + Mathf.Max(0f, weights.z);
        if (total <= 0f) return 1;
        float ticket = (float)turnRandom.NextDouble() * total;
        return ticket < one ? 1 : ticket < one + two ? 2 : 3;
    }

    private int Roll(int min, int max) => turnRandom.Next(Math.Min(min, max), Math.Max(min, max) + 1);
    private float Roll(float min, float max) => Mathf.Lerp(Mathf.Min(min, max),
        Mathf.Max(min, max), (float)turnRandom.NextDouble());

    private void PrepareTurn()
    {
        turnSeed = seedOverride != 0 ? seedOverride : UnityEngine.Random.Range(1, int.MaxValue);
        turnRandom = new System.Random(turnSeed);
        int totalWeight = 0;
        foreach (ComboCrewTurnProfile profile in turnProfiles)
            if (profile != null) totalWeight += Mathf.Max(0, profile.selectionWeight);
        int ticket = turnRandom.Next(totalWeight);
        foreach (ComboCrewTurnProfile profile in turnProfiles)
        {
            if (profile == null) continue;
            ticket -= Mathf.Max(0, profile.selectionWeight);
            if (ticket < 0) { turnProfile = profile; break; }
        }
        turnElapsed = 0f;
        stationIncidents = new Incident[stations.Length];
        stationTimers = new float[stations.Length];
        stationRepairDurations = new float[stations.Length];
        stationReadyAt = new float[stations.Length];
        workerWarnings = new float[workers.Length];
        workerBreakDurations = new float[workers.Length];
        workerReadyAt = new float[workers.Length];
        workerAway = new bool[workers.Length];
        workerBreakPending = new bool[workers.Length];
        workerAvailable = new bool[workers.Length];
        workerAvailable[0] = true;
        int count = Mathf.Clamp(Roll(turnProfile.availableWorkers.x,
            turnProfile.availableWorkers.y), MainStationCount, Mathf.Min(5, workers.Length));
        int[] candidates = new int[workers.Length - 1];
        for (int i = 0; i < candidates.Length; i++) candidates[i] = i + 1;
        bool feasible = false;
        for (int attempt = 0; attempt < 24 && !feasible; attempt++)
        {
            for (int i = candidates.Length - 1; i > 0; i--)
            {
                int j = turnRandom.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            for (int i = 0; i < candidates.Length; i++)
                workerAvailable[candidates[i]] = i < count - 1;
            feasible = CrewCanKeepPace();
        }
        if (!feasible)
            Debug.LogError("Combo Crew: nenhum sorteio de equipe manteve o ritmo máximo dos pedidos. Revise os perfis de funcionários ou de turno.", this);
        crewFeasible = feasible;
        for (int i = 0; i < candidates.Length; i++)
        {
            bool available = i < count - 1;
            int worker = candidates[i];
            workerAvailable[worker] = available;
            workerCards[worker].gameObject.SetActive(available);
            // Disponibilidade controla o card. O ator só aparece quando Place recebe uma bancada.
            workers[worker].gameObject.SetActive(false);
        }
        workers[0].gameObject.SetActive(false);
        int slot = 0;
        float cardWidth = count == 4 ? 210f : 176f;
        float cardSpacing = count == 4 ? 230f : 185f;
        for (int i = 0; i < workers.Length; i++)
        {
            workerCards[i].SetAssigned(false);
            if (!workerAvailable[i]) continue;
            Vector2 position = new Vector2((slot - (count - 1) * .5f) * cardSpacing, 20f);
            cardAnchors[i].anchoredPosition = position;
            cardAnchors[i].sizeDelta = new Vector2(cardWidth, 310f);
            workerCards[i].DragVisual.anchoredPosition = position;
            workerCards[i].SetCardLayout(cardWidth, 310f);
            slot++;
        }

        int failures = Mathf.Clamp(Roll(turnProfile.stationFailures.x,
            turnProfile.stationFailures.y), 0, 8);
        int breaks = Mathf.Clamp(Roll(turnProfile.workerBreaks.x,
            turnProfile.workerBreaks.y), 0, 8);
        turnEvents = new TurnEvent[failures + breaks];
        int index = 0;
        for (int i = 0; i < failures; i++)
            turnEvents[index++] = new TurnEvent { At = EventTime(i, failures),
                Duration = Mathf.Max(.1f, Roll(turnProfile.repairSeconds.x, turnProfile.repairSeconds.y)) };
        for (int i = 0; i < breaks; i++)
            turnEvents[index++] = new TurnEvent { IsBreak = true,
                At = EventTime(i, breaks),
                Duration = Mathf.Max(.1f, Roll(turnProfile.breakSeconds.x, turnProfile.breakSeconds.y)) };
    }

    private float EventTime(int index, int count)
    {
        float window = Mathf.Max(1f, matchSeconds - 16f);
        return 8f + window * (index + .2f + .6f * (float)turnRandom.NextDouble()) / count;
    }

    private void UpdateEvents(float delta)
    {
        turnElapsed += delta;
        if (!closing)
            foreach (TurnEvent planned in turnEvents)
            {
                if (planned.Resolved || turnElapsed < planned.At) continue;
                bool started = planned.IsBreak ? ScheduleBreak(planned.Duration)
                    : ScheduleFailure(planned.Duration);
                if (started || turnElapsed > planned.At + 9f)
                {
                    planned.Resolved = true;
                    if (!started) EventLogger.Instance?.RecordActivityEvent("combo_event_skipped");
                }
            }

        for (int station = 0; station < stations.Length; station++)
        {
            switch (stationIncidents[station])
            {
                case Incident.Warning:
                    stationTimers[station] -= delta;
                    int worker = stationWorker[station];
                    if (stationTimers[station] > 0f || worker >= 0
                        && (moving[worker] || workers[worker].Busy)) break;
                    stationIncidents[station] = Incident.Broken;
                    stations[station].SetAlert(ComboCrewStationView.Alert.Broken);
                    ShowMessage($"Toque em {StationNames[station]} para reparar", 3f);
                    AudioManager.Instance?.PlayStationBreak();
                    MequiHaptics.LightImpact();
                    EventLogger.Instance?.RecordActivityEvent("combo_station_problem");
                    break;
                case Incident.Repairing:
                    stationTimers[station] = Mathf.Max(0f, stationTimers[station] - delta);
                    if (stationTimers[station] > 0f) break;
                    stationIncidents[station] = Incident.None;
                    stationReadyAt[station] = turnElapsed + Mathf.Max(0f, turnProfile.stationCooldown);
                    stations[station].SetAlert(ComboCrewStationView.Alert.None);
                    stations[station].ResetProgress();
                    ShowMessage($"Estação {StationNames[station]} pronta", 1.5f);
                    AudioManager.Instance?.PlayStationReady();
                    MequiHaptics.Confirm();
                    EventLogger.Instance?.RecordActivityEvent("combo_station_repaired");
                    break;
            }
        }

        for (int worker = 0; worker < workers.Length; worker++)
        {
            if (!workerBreakPending[worker]) continue;
            workerWarnings[worker] = Mathf.Max(0f, workerWarnings[worker] - delta);
            workers[worker].ShowBreakWarning(workerWarnings[worker] / 5f);
            if (closing)
            {
                workerWarnings[worker] = 0f;
                workerBreakPending[worker] = false;
                workers[worker].ShowBreakWarning(0f);
                continue;
            }
            if (workerWarnings[worker] > 0f || moving[worker]
                || workers[worker].Busy || workers[worker].Dragging) continue;
            int index = worker;
            if (!workers[index].TakeBreak(
                workerBreakDurations[index] * workers[index].BreakDurationMultiplier,
                () => ReturnWorker(index))) continue;
            workerWarnings[index] = 0f;
            workerBreakPending[index] = false;
            workerAway[index] = true;
            moving[index] = true;
            ShowMessage("Pessoa da equipe em pausa: reorganize", 3f);
            EventLogger.Instance?.RecordActivityEvent("combo_worker_absent");
        }
    }

    private bool ScheduleFailure(float duration)
    {
        if (!HasRoomForDisruption()) return false;
        int active = 0;
        foreach (Incident state in stationIncidents) if (state != Incident.None) active++;
        if (active >= turnProfile.maxConcurrentFailures) return false;
        int start = turnRandom.Next(stations.Length);
        for (int i = 0; i < stations.Length; i++)
        {
            int station = (start + i) % stations.Length;
            if (stationIncidents[station] != Incident.None || turnElapsed < stationReadyAt[station]) continue;
            stationIncidents[station] = Incident.Warning;
            stationTimers[station] = repairWarningSeconds;
            stationRepairDurations[station] = duration;
            stations[station].SetAlert(ComboCrewStationView.Alert.Warning);
            ShowMessage($"{StationNames[station]}: ajuste em breve", 2.5f);
            EventLogger.Instance?.RecordActivityEvent($"combo_failure_warning_{station}");
            return true;
        }
        return false;
    }

    private bool ScheduleBreak(float duration)
    {
        if (!HasRoomForDisruption()) return false;
        int active = 0;
        for (int i = 0; i < workers.Length; i++)
            if (workerAway[i] || workerBreakPending[i]) active++;
        if (active >= turnProfile.maxConcurrentBreaks) return false;
        float total = 0f;
        for (int i = 0; i < workers.Length; i++)
            if (workerAvailable[i] && assigned[i] >= 0 && !workerAway[i]
                && !workerBreakPending[i]
                && turnElapsed >= workerReadyAt[i] && !moving[i]
                && !workers[i].Busy && !workers[i].Dragging)
                total += workers[i].PauseWeight;
        if (total <= 0f) return false;
        float ticket = (float)turnRandom.NextDouble() * total;
        for (int i = 0; i < workers.Length; i++)
        {
            if (!workerAvailable[i] || assigned[i] < 0 || workers[i].PauseWeight <= 0f
                || workerAway[i]
                || workerBreakPending[i] || turnElapsed < workerReadyAt[i]
                || moving[i] || workers[i].Busy || workers[i].Dragging) continue;
            ticket -= workers[i].PauseWeight;
            if (ticket > 0f) continue;
            workerWarnings[i] = 5f;
            workerBreakPending[i] = true;
            workerBreakDurations[i] = duration;
            breakReorganizationLogged = false;
            ShowMessage("Pausa em 5 segundos: reorganize a equipe", 3f);
            EventLogger.Instance?.RecordActivityEvent($"combo_break_warning_{i}");
            return true;
        }
        return false;
    }

    private void ReturnWorker(int worker)
    {
        moving[worker] = false;
        workerAway[worker] = false;
        if (assigned[worker] < 0)
        {
            workers[worker].gameObject.SetActive(false);
            workerCards[worker].SetAssigned(false);
        }
        workerReadyAt[worker] = turnElapsed + Mathf.Max(0f, turnProfile.workerCooldown);
        ShowMessage("Pessoa da equipe voltou", 1.5f);
        AudioManager.Instance?.PlayReady();
        EventLogger.Instance?.RecordActivityEvent("combo_worker_returned");
        RefreshHud();
        TryFinish();
    }

    public void TryRepairStation(int station)
    {
        if (!playing || leaving || station < 0 || station >= stations.Length
            || stationIncidents[station] != Incident.Broken) return;
        stationIncidents[station] = Incident.Repairing;
        int worker = stationWorker[station];
        float multiplier = worker >= 0 && !IsUnavailable(worker)
            ? workers[worker].RepairTimeMultiplier : 1f;
        stationRepairDurations[station] *= multiplier;
        stationTimers[station] = stationRepairDurations[station];
        stations[station].SetAlert(ComboCrewStationView.Alert.Repairing);
        stations[station].ResetProgress();
        ShowMessage("Manutenção iniciada", 1.5f);
        AudioManager.Instance?.PlayRepair();
        MequiHaptics.Selection();
        EventLogger.Instance?.RecordUserAction("combo_repair_station");
    }
}
