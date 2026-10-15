using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Pedidos, funcionários e bandejas do Combo Crew; todos os visuais são da Hierarchy.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewController : MonoBehaviour
{
    private const int MainStationCount = 4;
    private const int WashStation = 4;
    private static readonly string[] StationNames = { "PREPARO", "CHAPA", "MONTAGEM", "ENTREGA", "LAVA-LOUÇA" };
    private const float RepairWarningAt = .65f;
    private const float AbsenceAt = .5f;
    private enum OrderState { Free, Working, Travelling, Resolving }
    private enum Incident { None, Warning, Broken, Repairing, Done }
    private sealed class Order
    {
        public OrderState State;
        public int Sequence, Stage, Items;
        public float Patience, Remaining, Progress;
        public float ReadyHold;
        public bool HasTray;
    }

    [Header("Sessão e HUD")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private ResultPopup resultPopup;
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject preparationPanel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private TMP_Text cleanTrayLabel;
    [SerializeField] private TMP_Text dirtyTrayLabel;
    [SerializeField] private ComboCrewClipboard clipboard;
    [SerializeField] private TMP_Text preparationHint;
    [SerializeField] private Image clipboardHighlight;
    [Header("Animação do aviso da prancheta")]
    [SerializeField, Min(.01f)] private float clipboardPulseSeconds = .9f;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private TMP_Text profileTitle;
    [SerializeField] private TMP_Text profileDescription;
    [SerializeField] private Button closeProfileButton;
    [SerializeField, Min(1f)] private float matchSeconds = 70f;
    [SerializeField, Min(.1f)] private float spawnInterval = 6f;
    [SerializeField, Min(1f)] private float patienceSeconds = 42f;
    [SerializeField, Min(.1f)] private float stageSeconds = 4f;
    [SerializeField, Min(1)] private int initialTrays = 4;
    [SerializeField, Min(.1f)] private float washSecondsPerTray = 1.4f;
    [SerializeField, Min(0)] private int completionPoints = 20;
    [SerializeField, Min(0f)] private float repairWarningSeconds = 3f;
    [SerializeField, Min(.1f)] private float repairSeconds = 3.5f;
    [SerializeField, Min(1f)] private float absenceSeconds = 15f;

    [Header("Referências montadas no Canvas")]
    [Tooltip("Ordem: Preparo, Chapa, Montagem, Entrega, Lava-louça. A posição visual pode ser em S.")]
    [SerializeField] private ComboCrewStationView[] stations;
    [Tooltip("Índice zero é o personagem do jogador; os demais são funcionários.")]
    [SerializeField] private ComboCrewWorkerView[] workers;
    [Tooltip("Um card por personagem; o índice zero representa o jogador.")]
    [SerializeField] private ComboCrewWorkerCard[] workerCards;
    [SerializeField] private RectTransform[] cardAnchors;
    [SerializeField] private ComboCrewOrderView[] orderCards;
    [SerializeField] private RectTransform[] orderAnchors;
    [SerializeField] private RectTransform departureAnchor;

    private Order[] orders;
    private int[] assigned;
    private int[] stationWorker;
    private bool[] moving;
    private float matchRemaining, nextSpawn, messageRemaining, repairDuration;
    private int nextSequence, delivered, missed, cleanTrays, dirtyTrays;
    private bool preparing, playing, closing, settled, leaving, paused, logged;
    private int washingCount;
    private bool washCounterReserved;
    private int repairStation = -1;
    private Incident incident;
    private float incidentRemaining, patienceGrace, absenceWarningRemaining;
    private int absentWorker = -1;
    private bool breakReorganizationLogged;
    private int absencePendingWorker = -1;
    private int problemWorker = -1;
    private bool absenceTriggered;
    private Tween clipboardPulse;
    private Image[] clipboardEdges;
    private RectTransform clipboardHandle;
    private Vector2 clipboardHandlePosition;
    private Vector3 clipboardHandleScale;
    private bool clipboardGuidanceComplete;
    private RectTransform profileRect;
    private RectTransform profileSource;
    private Vector3 profileHomePosition;
    private Vector3 profileHomeScale;
    private float profileSourceScale = .3f;
    private Tween profileTween;

    public bool CanMoveWorker(int index) => (preparing || playing) && !leaving && !settled
        && index >= 0 && index < workers.Length && workers[index] != null
        && index != absentWorker && !workers[index].Busy;
    public bool CanNudgeWorkerCards => preparing && clipboard != null && clipboard.IsOpen
        && (profilePanel == null || !profilePanel.activeSelf);
    public RectTransform CardHome(int index) => cardAnchors != null && index >= 0
        && index < cardAnchors.Length ? cardAnchors[index] : null;
    public string WorkerTrait(int index) => workers != null && index >= 0
        && index < workers.Length ? workers[index].ShortTrait : string.Empty;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(StartMatch);
        if (backButton != null) backButton.onClick.AddListener(BackToHub);
        if (closeProfileButton != null) closeProfileButton.onClick.AddListener(CloseProfile);
        if (preparationPanel != null) preparationPanel.SetActive(true);
        if (profilePanel != null) profilePanel.SetActive(false);
        if (profilePanel != null)
        {
            profileRect = profilePanel.transform.Find("PainelFicha") as RectTransform;
            if (profileRect != null)
            {
                profileHomePosition = profileRect.position;
                profileHomeScale = profileRect.localScale;
            }
        }
    }

    private void OnEnable() => SceneLoader.SceneLeaving += SceneLeaving;

    private void Start()
    {
        UnityEngine.Device.Screen.orientation = ScreenOrientation.Portrait;
        Canvas.ForceUpdateCanvases();
        if (profileRect != null) profileHomePosition = profileRect.position;
        if (!Validate()) { ShowMessage("Configuração incompleta. Consulte o Console."); return; }
        orders = new Order[orderCards.Length];
        for (int i = 0; i < orders.Length; i++)
        {
            orders[i] = new Order();
            orderCards[i].gameObject.SetActive(false);
        }
        assigned = new int[workers.Length];
        moving = new bool[workers.Length];
        stationWorker = new int[stations.Length];
        for (int i = 0; i < workers.Length; i++) assigned[i] = -1;
        for (int i = 0; i < stations.Length; i++)
        {
            stationWorker[i] = -1;
            stations[i].Bind(this, i);
        }
        cleanTrays = initialTrays;
        incident = Incident.None;
        workers[0].ApplyPlayerAppearance();
        preparing = true;
        CreateClipboardEdges();
        RefreshHud();
    }

    private bool Validate()
    {
        bool valid = sceneLoader != null && resultPopup != null
            && startButton != null && backButton != null && preparationPanel != null
            && timerLabel != null && messageLabel != null
            && cleanTrayLabel != null && dirtyTrayLabel != null
            && clipboard != null && clipboard.Configured
            && stations != null && stations.Length == 5
            && workers != null && workers.Length >= 2
            && workerCards != null && workerCards.Length == workers.Length
            && cardAnchors != null && cardAnchors.Length == workers.Length
            && orderCards != null && orderCards.Length >= 2
            && orderAnchors != null && orderAnchors.Length == orderCards.Length
            && departureAnchor != null
            && PlayerManager.Instance != null && PlayerManager.Instance.Profile != null
            && PointsService.Instance != null && EventLogger.Instance != null;
        if (valid)
            for (int i = 0; i < stations.Length; i++)
                if (stations[i] == null || !stations[i].Configured) { valid = false; break; }
        if (valid)
            for (int i = 0; i < workers.Length; i++)
                if (workers[i] == null || !workers[i].Configured || cardAnchors[i] == null
                    || workerCards[i] == null || !workerCards[i].Configured)
                { valid = false; break; }
        if (valid)
            valid = !(matchSeconds <= 0f || spawnInterval <= 0f || patienceSeconds <= 0f
                || stageSeconds <= 0f || initialTrays < 1 || washSecondsPerTray <= 0f);
        if (valid)
            for (int i = 0; i < orderCards.Length; i++)
                if (orderCards[i] == null || !orderCards[i].Configured || orderAnchors[i] == null)
                { valid = false; break; }
        if (!valid && startButton != null) startButton.interactable = false;
        return valid;
    }

    private void Update()
    {
        if (leaving || settled || paused || SceneLoader.IsTransitionInProgress || orders == null) return;
        float delta = Time.deltaTime;
        if (messageRemaining > 0f && (messageRemaining -= delta) <= 0f)
            messageLabel.text = string.Empty;
        if (preparing)
        {
            RefreshHud();
            UpdateClipboardPulse();
            return;
        }
        if (!playing) return;
        if (!closing)
        {
            matchRemaining = Mathf.Max(0f, matchRemaining - delta);
            nextSpawn -= delta;
            if (nextSpawn <= 0f && matchRemaining > 0f && TrySpawn()) nextSpawn = spawnInterval;
            if (matchRemaining <= 0f)
            {
                closing = true;
                EventLogger.Instance?.MarkTimeLimitReached();
                ShowMessage("Finalizando pedidos", 3f);
            }
        }
        UpdateEvents(delta);
        bool protectPatience = patienceGrace > 0f;
        patienceGrace = Mathf.Max(0f, patienceGrace - delta);
        for (int i = 0; i < orders.Length; i++)
        {
            Order order = orders[i];
            if (order.State == OrderState.Free || order.State == OrderState.Resolving) continue;
            // A entrega já saiu da bancada: o pedido não pode expirar durante a rota.
            if (!protectPatience && !(order.State == OrderState.Travelling && order.Stage == 3))
                order.Remaining = Mathf.Max(0f, order.Remaining - delta);
            if (order.Remaining <= 0f && !(order.State == OrderState.Travelling && order.Stage == 3))
            { Resolve(i, false); continue; }
            if (order.State == OrderState.Working) Advance(i, delta);
        }
        if (washingCount == 0 && dirtyTrays > 0) StartWashing();
        RefreshHud();
        if (closing) TryFinish();
    }

    public void StartMatch()
    {
        if (!preparing || orders == null || leaving || !ReadyToStart()) return;
        preparing = false;
        playing = true;
        if (clipboard.IsOpen) clipboard.Toggle();
        SetClipboardPulse(false);
        preparationPanel.SetActive(false);
        startButton.gameObject.SetActive(false);
        foreach (var station in stations) station.SetProgressVisible(true);
        matchRemaining = matchSeconds;
        nextSpawn = spawnInterval;
        EventLogger.Instance.BeginSession("combo_crew");
        logged = true;
        EventLogger.Instance.RecordUserAction("combo_start");
        breakReorganizationLogged = false;
        int specialistPlacements = 0;
        int specialists = 0;
        for (int employee = 0; employee < workers.Length; employee++)
        {
            float best = 1f;
            for (int stage = 0; stage < stations.Length; stage++) best = Mathf.Max(best, workers[employee].SpeedAt(stage));
            if (best <= 1f) continue;
            specialists++;
            int assignedStation = assigned[employee];
            if (assignedStation >= 0 && workers[employee].SpeedAt(assignedStation) >= best - .001f) specialistPlacements++;
        }
        EventLogger.Instance.RecordActivityEvent("combo_initial_specialists:" + specialistPlacements + ":" + specialists);
        TrySpawn();
        AudioManager.Instance?.PlayConfirm();
        RefreshHud();
    }

    private bool TrySpawn()
    {
        for (int i = 0; i < orders.Length; i++)
        {
            if (orders[i].State != OrderState.Free) continue;
            Order order = orders[i];
            order.State = OrderState.Working;
            order.Sequence = nextSequence++;
            order.Stage = 0;
            // A máscara é compartilhada com o card e com a bandeja: hambúrguer, bebida, batata.
            order.Items = order.Sequence % 3 == 0 ? 1 : order.Sequence % 3 == 1 ? 5 : 7;
            order.Patience = patienceSeconds;
            order.Remaining = order.Patience;
            order.Progress = 0f;
            order.ReadyHold = 0f;
            order.HasTray = false;
            orderCards[i].Show(order.Items, orderAnchors[Rank(i)]);
            EventLogger.Instance?.RecordActivityEvent("combo_order_created");
            return true;
        }
        return false;
    }

    private void Advance(int id, float delta)
    {
        Order order = orders[id];
        int stage = order.Stage;
        int employee = stationWorker[stage];
        if (employee < 0 || employee == absentWorker || moving[employee] || workers[employee].Busy
            || (stage == repairStation && (incident == Incident.Broken || incident == Incident.Repairing))
            || FirstAtStage(stage) != id) return;
        float speed = workers[employee].SpeedAt(stage);
        if (speed <= 0f) return;
        if (order.Progress < 1f)
            order.Progress = Mathf.Min(1f, order.Progress + delta * speed / stageSeconds);
        if (order.Progress >= 1f) order.ReadyHold += delta;
        if (order.Progress < 1f || order.ReadyHold < .18f || (stage == 0 && cleanTrays < 1)) return;
        if (stage < MainStationCount - 1 && StageOccupied(stage + 1, id)) return;
        if (stage == 0) cleanTrays--;
        order.HasTray = true;
        order.State = OrderState.Travelling;
        moving[employee] = true;
        if (stage == MainStationCount - 1)
        {
            bool deliveredAtDestination = false;
            workers[employee].SetTrayItems(order.Items);
            workers[employee].Deliver(departureAnchor, () =>
            {
                if (!playing || order.State != OrderState.Travelling) return;
                deliveredAtDestination = true;
                Resolve(id, true, false);
            }, () =>
            {
                moving[employee] = false;
                if (!deliveredAtDestination) return;
                workers[employee].SetTrayItems(0);
                dirtyTrays++;
                RefreshHud();
                TryFinish();
            });
            return;
        }
        workers[employee].Travel(stations[stage + 1].HandoffAnchor, () =>
        {
            if (!playing || order.State != OrderState.Travelling) return;
            order.Stage = stage + 1;
            order.Progress = 0f;
            order.ReadyHold = 0f;
            order.State = OrderState.Working;
            stations[stage].ResetProgress();
            AudioManager.Instance?.PlayConfirm();
        }, () => moving[employee] = false);
    }

    private int FirstAtStage(int stage)
    {
        int result = -1, sequence = int.MaxValue;
        for (int i = 0; i < orders.Length; i++)
            if (orders[i].State == OrderState.Working && orders[i].Stage == stage
                && orders[i].Sequence < sequence) { result = i; sequence = orders[i].Sequence; }
        return result;
    }

    private int VisibleAtStage(int stage)
    {
        int id = FirstAtStage(stage);
        if (id >= 0) return id;
        for (int i = 0; i < orders.Length; i++)
            if (orders[i].State == OrderState.Travelling && orders[i].Stage == stage) return i;
        return -1;
    }

    private bool StageOccupied(int stage, int except)
    {
        if (stage == MainStationCount - 1 && washCounterReserved) return true;
        for (int i = 0; i < orders.Length; i++)
            if (i != except && (orders[i].State == OrderState.Working || orders[i].State == OrderState.Travelling)
                && orders[i].Stage == stage) return true;
        return false;
    }

    private void UpdateEvents(float delta)
    {
        if (!closing && incident == Incident.None && matchRemaining <= matchSeconds * RepairWarningAt)
        {
            repairStation = Random.Range(0, stations.Length);
            incident = Incident.Warning;
            incidentRemaining = repairWarningSeconds;
            stations[repairStation].SetAlert(ComboCrewStationView.Alert.Warning);
            ShowMessage($"{StationNames[repairStation]}: ajuste em breve", 2.5f);
        }
        if (incident == Incident.Warning)
        {
            incidentRemaining -= delta;
            int employee = stationWorker[repairStation];
            if (incidentRemaining <= 0f && (employee < 0 || (!moving[employee] && !workers[employee].Busy)))
            {
                incident = Incident.Broken;
                patienceGrace = 3f;
                stations[repairStation].SetAlert(ComboCrewStationView.Alert.Broken);
                ShowMessage($"Toque na estação {StationNames[repairStation]} para reparar", 3f);
                AudioManager.Instance?.PlayError();
                EventLogger.Instance?.RecordActivityEvent("combo_station_problem");
            }
        }
        else if (incident == Incident.Repairing)
        {
            incidentRemaining = Mathf.Max(0f, incidentRemaining - delta);
            if (incidentRemaining <= 0f)
            {
                incident = Incident.Done;
                stations[repairStation].SetAlert(ComboCrewStationView.Alert.None);
                stations[repairStation].ResetProgress();
                ShowMessage($"Estação {StationNames[repairStation]} pronta", 1.5f);
                AudioManager.Instance?.PlayReady();
                EventLogger.Instance?.RecordActivityEvent("combo_station_repaired");
            }
        }

        if (!closing && !absenceTriggered && absencePendingWorker < 0
            && matchRemaining <= matchSeconds * AbsenceAt)
        {
            float totalWeight = 0f;
            for (int stage = 0; stage < stations.Length; stage++)
            {
                int employee = stationWorker[stage];
                if (employee >= 0 && !moving[employee] && !workers[employee].Busy
                    && !workers[employee].Dragging) totalWeight += workers[employee].PauseWeight;
            }
            float pick = Random.value * totalWeight;
            for (int stage = 0; stage < stations.Length && totalWeight > 0f; stage++)
            {
                int employee = stationWorker[stage];
                if (employee < 0 || moving[employee] || workers[employee].Busy
                    || workers[employee].Dragging || workers[employee].PauseWeight <= 0f) continue;
                pick -= workers[employee].PauseWeight;
                if (pick > 0f) continue;
                absencePendingWorker = employee;
                breakReorganizationLogged = false;
                absenceWarningRemaining = 5f;
                ShowMessage("Pausa em 5 segundos: reorganize a equipe", 3f);
                break;
            }
        }
        if (absencePendingWorker >= 0 && !closing)
        {
            absenceWarningRemaining = Mathf.Max(0f, absenceWarningRemaining - delta);
            int employee = absencePendingWorker;
            workers[employee].ShowBreakWarning(absenceWarningRemaining / 5f);
            if (absenceWarningRemaining <= 0f && !moving[employee]
                && !workers[employee].Busy && !workers[employee].Dragging
                && workers[employee].TakeBreak(absenceSeconds, () =>
                {
                    moving[employee] = false;
                    absentWorker = -1;
                    ShowMessage("Equipe completa novamente", 1.5f);
                    AudioManager.Instance?.PlayReady();
                    EventLogger.Instance?.RecordActivityEvent("combo_worker_returned");
                    RefreshHud();
                    TryFinish();
                }))
            {
                absentWorker = employee;
                absencePendingWorker = -1;
                absenceTriggered = true;
                moving[employee] = true;
                patienceGrace = Mathf.Max(patienceGrace, 2f);
                ShowMessage("Pessoa da equipe em pausa: reorganize", 3f);
                EventLogger.Instance?.RecordActivityEvent("combo_worker_absent");
            }
        }
        else if (closing && absencePendingWorker >= 0)
        {
            workers[absencePendingWorker].ShowBreakWarning(0f);
            absencePendingWorker = -1;
        }
    }

    public void TryRepairStation(int station)
    {
        if (!playing || leaving || station != repairStation || incident != Incident.Broken) return;
        incident = Incident.Repairing;
        int employee = stationWorker[station];
        repairDuration = repairSeconds * (employee >= 0 && employee != absentWorker
            ? workers[employee].RepairTimeMultiplier : 1f);
        incidentRemaining = repairDuration;
        stations[station].SetAlert(ComboCrewStationView.Alert.Repairing);
        stations[station].ResetProgress();
        ShowMessage("Manutenção iniciada", 1.5f);
        AudioManager.Instance?.PlayRepair();
        MequiHaptics.Selection();
        EventLogger.Instance?.RecordUserAction("combo_repair_station");
    }

    private void StartWashing()
    {
        if (repairStation == WashStation && (incident == Incident.Broken || incident == Incident.Repairing)) return;
        // Aguarda o balcão liberar o pedido em curso antes de buscar bandejas usadas.
        int counterEmployee = stationWorker[MainStationCount - 1];
        if (StageOccupied(MainStationCount - 1, -1)
            || (counterEmployee >= 0 && workers[counterEmployee].Busy)) return;
        for (int i = 0; i < orders.Length; i++)
            if (orders[i].State == OrderState.Travelling
                && orders[i].Stage == MainStationCount - 2) return;
        int employee = stationWorker[WashStation];
        if (employee < 0 || employee == absentWorker || workers[employee].Busy || moving[employee]) return;
        int batch = dirtyTrays;
        washingCount = batch;
        washCounterReserved = true;
        moving[employee] = true;
        workers[employee].Wash(stations[3].HandoffAnchor, stations[0].HandoffAnchor,
            batch, washSecondsPerTray / workers[employee].SpeedAt(WashStation), () =>
            {
                washCounterReserved = false;
                dirtyTrays -= batch;
                RefreshHud();
            }, () =>
            {
                washCounterReserved = false;
                moving[employee] = false;
                cleanTrays += washingCount;
                washingCount = 0;
                AudioManager.Instance?.PlayReady();
                RefreshHud();
                TryFinish();
            });
    }

    private void Resolve(int id, bool success, bool trayReturned = true)
    {
        Order order = orders[id];
        if (order.State == OrderState.Free || order.State == OrderState.Resolving) return;
        order.State = OrderState.Resolving;
        if (success) { delivered++; if (trayReturned) dirtyTrays++; AudioManager.Instance?.PlayConfirm(); }
        else { missed++; if (order.HasTray) dirtyTrays++; AudioManager.Instance?.PlayError(); }
        EventLogger.Instance?.RecordActivityEvent(success ? "combo_order_delivered" : "combo_order_missed");
        orderCards[id].Resolve(success, () =>
        {
            order.State = OrderState.Free;
            order.HasTray = false;
            order.Progress = 0f;
            TryFinish();
        });
        RefreshHud();
    }

    private int Rank(int id)
    {
        int rank = 0;
        for (int i = 0; i < orders.Length; i++)
            if (i != id && orders[i].State != OrderState.Free
                && orders[i].Sequence < orders[id].Sequence) rank++;
        return rank;
    }

    public bool AssignWorker(int worker, int station)
    {
        if ((!preparing && !playing) || worker < 0 || worker >= workers.Length
            || worker == absentWorker || station < 0 || station >= stations.Length
            || workers[worker].Busy || moving[worker]) return false;
        int old = assigned[worker];
        if (old == station) return true;
        int occupant = stationWorker[station];
        if (occupant >= 0 && occupant != worker)
        {
            if (old < 0 || (occupant != absentWorker && (workers[occupant].Busy || moving[occupant])))
            { ShowMessage("Esta bancada já tem alguém."); return false; }
            assigned[occupant] = old;
            workers[occupant].Place(stations[old].WorkAnchor, false);
        }
        if (old >= 0) stationWorker[old] = occupant;
        assigned[worker] = station;
        stationWorker[station] = worker;
        clipboardGuidanceComplete = true;
        SetClipboardPulse(false);
        workers[worker].Place(stations[station].WorkAnchor, false);
        if (preparing) RefreshHud();
        EventLogger.Instance?.RecordUserAction(worker == 0 ? "combo_player_move" : "combo_assign_worker");
        if (playing && !breakReorganizationLogged && (absencePendingWorker >= 0 || absentWorker >= 0))
        {
            breakReorganizationLogged = true;
            EventLogger.Instance?.RecordActivityEvent("combo_break_reorganized");
        }
        AudioManager.Instance?.PlayConfirm();
        MequiHaptics.Selection();
        HighlightStations(false);
        return true;
    }

    public void SnapWorkerToStation(int worker)
    {
        if (assigned == null || worker < 0 || worker >= assigned.Length) return;
        int station = assigned[worker];
        if (station >= 0 && station < stations.Length)
            workers[worker].Place(stations[station].WorkAnchor, true);
    }

    public void HighlightStations(bool visible, int draggedWorker = -1)
    {
        if (stations == null || stationWorker == null) return;
        for (int i = 0; i < stations.Length; i++)
        {
            int occupant = stationWorker[i];
            bool canSwap = draggedWorker >= 0 && assigned[draggedWorker] >= 0
                && occupant >= 0 && occupant != absentWorker
                && !workers[occupant].Busy && !moving[occupant];
            stations[i].Highlight(visible && (occupant < 0 || canSwap));
        }
    }

    private void TryFinish()
    {
        if (!closing || settled || washingCount > 0) return;
        for (int i = 0; i < moving.Length; i++) if (moving[i]) return;
        for (int i = 0; i < orders.Length; i++) if (orders[i].State != OrderState.Free) return;
        settled = true;
        playing = false;
        backButton.interactable = false;
        int points = PointsService.Instance.AwardParticipation(completionPoints);
        PlayerManager.Instance.SetPendingPoints(points, "ComboCrew");
        EventLogger.Instance?.RecordActivityEvent($"combo_result_{delivered}_{missed}");
        EventLogger.Instance?.CompleteSession();
        logged = false;
        AudioManager.Instance?.PlayCompletion();
        resultPopup.Show(points, delivered, missed);
    }

    private void RefreshHud()
    {
        if (timerLabel != null)
        {
            if (preparing) timerLabel.text = "PRONTO";
            else
            {
                int seconds = Mathf.CeilToInt(matchRemaining);
                timerLabel.SetText("{0}:{1:00}", seconds / 60, seconds % 60);
            }
        }
        if (preparing)
        {
            int staffed = 0;
            for (int i = 0; i < stationWorker.Length; i++) if (stationWorker[i] >= 0) staffed++;
            if (preparationHint != null)
                preparationHint.text = "POSICIONE AO MENOS 1 PERSONAGEM";
            if (startButton != null) startButton.interactable = staffed > 0;
        }
        if (cleanTrayLabel != null) cleanTrayLabel.SetText("LIMPAS {0}/{1}", cleanTrays, initialTrays);
        if (dirtyTrayLabel != null) dirtyTrayLabel.SetText("SUJAS {0}", dirtyTrays);
        if (orders == null) return;
        UpdateProblemWorker();
        for (int i = 0; i < orders.Length; i++)
        {
            Order order = orders[i];
            if (order.State != OrderState.Free && order.State != OrderState.Resolving)
                orderCards[i].Refresh(order.Remaining / order.Patience, orderAnchors[Rank(i)]);
        }
        for (int stage = 0; stage < stations.Length; stage++)
        {
            int id = VisibleAtStage(stage);
            if (stage == WashStation)
                stations[stage].Refresh(stage == repairStation && incident == Incident.Repairing
                        ? 1f - incidentRemaining / repairDuration
                        : stage == repairStation && incident == Incident.Broken ? 0f
                        : washingCount > 0 && stationWorker[stage] >= 0
                            ? workers[stationWorker[stage]].RouteProgress : 0f,
                    stage == repairStation && (incident == Incident.Warning || incident == Incident.Broken
                        || incident == Incident.Repairing) ? StationStatus(stage, id)
                        : stationWorker[stage] < 0 ? "Sem funcionário"
                        : stationWorker[stage] == absentWorker ? "Ausente"
                        : stationWorker[stage] == absencePendingWorker ? "Pausa em breve"
                        : washingCount > 0 ? "Lavando"
                        : dirtyTrays > 0 ? "Bandejas aguardando" : "Livre", false,
                    stationWorker[stage] >= 0 && workers[stationWorker[stage]].WashingAtSink);
            else
                stations[stage].Refresh(stage == repairStation && incident == Incident.Repairing
                        ? 1f - incidentRemaining / repairDuration : id < 0 ? 0f : orders[id].Progress,
                    StationStatus(stage, id),
                    (id >= 0 && orders[id].State == OrderState.Working
                        && (orders[id].HasTray || (stage == 0 && cleanTrays > 0)))
                    || (stage == 3 && dirtyTrays > 0));
        }
    }

    private void UpdateProblemWorker()
    {
        int current = incident == Incident.Broken && repairStation >= 0 ? stationWorker[repairStation] : -1;
        if (current == absentWorker || (current >= 0 && (moving[current] || workers[current].Busy)))
            current = -1;
        if (current == problemWorker) return;
        if (problemWorker >= 0) workers[problemWorker].ShowStationProblem(false);
        problemWorker = current;
        if (current >= 0) workers[current].ShowStationProblem(true);
    }

    private string StationStatus(int stage, int id)
    {
        int employee = stationWorker[stage];
        if (stage == repairStation)
        {
            if (incident == Incident.Warning) return "Precisa de ajuste";
            if (incident == Incident.Broken) return "Quebrada";
            if (incident == Incident.Repairing) return "Em manutenção";
        }
        if (employee < 0) return "Sem funcionário";
        if (employee == absentWorker) return "Ausente";
        if (employee == absencePendingWorker) return "Pausa em breve";
        if (id >= 0 && orders[id].State == OrderState.Travelling) return "Transportando";
        if (moving[employee]) return "Retornando";
        if (id < 0) return "Pronta";
        if (stage == 0 && cleanTrays < 1) return "Sem bandejas";
        if (stage < MainStationCount - 1 && orders[id].Progress >= 1f && StageOccupied(stage + 1, id))
            return "Aguardando bancada";
        return "Preparando";
    }

    private void ShowMessage(string text, float seconds = 2.5f)
    {
        if (messageLabel == null) return;
        messageLabel.text = text;
        messageRemaining = seconds;
    }

    private bool ReadyToStart()
    {
        if (stationWorker == null) return false;
        for (int i = 0; i < stationWorker.Length; i++)
            if (stationWorker[i] >= 0) return true;
        return false;
    }

    private void UpdateClipboardPulse()
    {
        for (int i = 0; i < stations.Length; i++)
            if (stationWorker[i] >= 0) { clipboardGuidanceComplete = true; break; }

        SetClipboardPulse(!clipboardGuidanceComplete && !clipboard.IsOpen);
    }

    private void SetClipboardPulse(bool enabled)
    {
        if (clipboardEdges == null || clipboardHandle == null) return;
        if (enabled && (clipboardPulse == null || !clipboardPulse.IsActive()))
        {
            Sequence wave = DOTween.Sequence().SetTarget(this);
            wave.Append(clipboardHandle.DOAnchorPosY(clipboardHandlePosition.y + 14f,
                clipboardPulseSeconds * .38f).SetEase(Ease.OutCubic));
            wave.Join(clipboardHandle.DOScale(clipboardHandleScale * 1.018f,
                clipboardPulseSeconds * .38f).SetEase(Ease.OutCubic));
            foreach (Image edge in clipboardEdges)
                wave.Join(edge.DOFade(.8f, clipboardPulseSeconds * .38f));
            wave.Append(clipboardHandle.DOAnchorPos(clipboardHandlePosition,
                clipboardPulseSeconds * .62f).SetEase(Ease.OutBack));
            wave.Join(clipboardHandle.DOScale(clipboardHandleScale,
                clipboardPulseSeconds * .62f).SetEase(Ease.OutCubic));
            foreach (Image edge in clipboardEdges)
                wave.Join(edge.DOFade(0f, clipboardPulseSeconds * .62f));
            clipboardPulse = wave.AppendInterval(1.1f).SetLoops(-1);
        }
        else if (!enabled && clipboardPulse != null)
        {
            clipboardPulse.Kill();
            clipboardPulse = null;
            clipboardHandle.anchoredPosition = clipboardHandlePosition;
            clipboardHandle.localScale = clipboardHandleScale;
            foreach (Image edge in clipboardEdges)
                edge.color = new Color(edge.color.r, edge.color.g, edge.color.b, 0f);
        }
    }

    private void CreateClipboardEdges()
    {
        if (clipboardHighlight == null) return;
        RectTransform frame = clipboardHighlight.rectTransform;
        clipboardHandle = frame;
        clipboardHandlePosition = frame.anchoredPosition;
        clipboardHandleScale = frame.localScale;
        clipboardEdges = new[]
        {
            CreateClipboardEdge(frame, "BordaSuperior", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 7f), new Vector2(0f, -3.5f)),
            CreateClipboardEdge(frame, "BordaDireita", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(7f, 0f), new Vector2(-3.5f, 0f)),
            CreateClipboardEdge(frame, "BordaInferior", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 7f), new Vector2(0f, 3.5f)),
            CreateClipboardEdge(frame, "BordaEsquerda", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(7f, 0f), new Vector2(3.5f, 0f))
        };
    }

    private static Image CreateClipboardEdge(RectTransform parent, string name,
        Vector2 min, Vector2 max, Vector2 size, Vector2 position)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = item.GetComponent<Image>();
        image.color = new Color(.18f, .13f, .03f, 0f);
        image.raycastTarget = false;
        return image;
    }

    public void OpenProfile(int index)
    {
        if (!preparing || profilePanel == null || index < 0 || index >= workers.Length) return;
        profileTween?.Kill();
        profileSource = workerCards != null && index < workerCards.Length && workerCards[index] != null
            ? workerCards[index].transform as RectTransform : null;
        profileTitle.text = workers[index].DisplayName;
        profileDescription.text = workers[index].Description;
        if (profileRect != null && profileSource != null)
        {
            float widthRatio = profileSource.rect.width * profileSource.lossyScale.x
                / Mathf.Max(1f, profileRect.rect.width * profileRect.lossyScale.x);
            float heightRatio = profileSource.rect.height * profileSource.lossyScale.y
                / Mathf.Max(1f, profileRect.rect.height * profileRect.lossyScale.y);
            profileSourceScale = Mathf.Clamp(Mathf.Min(widthRatio, heightRatio), .05f, 1f);
            profileRect.position = profileSource.position;
            profileRect.localScale = profileHomeScale * profileSourceScale;
        }
        profilePanel.SetActive(true);
        if (profileRect != null)
        {
            profileTween = DOTween.Sequence().SetTarget(this)
                .Append(profileRect.DOMove(profileHomePosition, .3f))
                .Join(profileRect.DOScale(profileHomeScale, .3f))
                .SetEase(Ease.OutCubic);
        }
    }

    public void CloseProfile()
    {
        if (profilePanel == null || !profilePanel.activeSelf) return;
        profileTween?.Kill();
        if (profileRect == null || profileSource == null)
        {
            profilePanel.SetActive(false);
            return;
        }
        profileTween = DOTween.Sequence().SetTarget(this)
            .Append(profileRect.DOMove(profileSource.position, .24f))
            .Join(profileRect.DOScale(profileHomeScale * profileSourceScale, .24f))
            .SetEase(Ease.InCubic)
            .OnComplete(() =>
            {
                profilePanel.SetActive(false);
                profileRect.position = profileHomePosition;
                profileRect.localScale = profileHomeScale;
                profileSource = null;
            });
    }

    public void BackToHub()
    {
        if (leaving || settled || sceneLoader == null) return;
        leaving = true;
        Abandon();
        sceneLoader.Load("HUB");
    }

    private void Abandon()
    {
        if (!logged) return;
        logged = false;
        EventLogger.Instance?.AbandonSession();
    }

    private void SceneLeaving(Scene scene)
    {
        if (scene != gameObject.scene) return;
        leaving = true;
        Abandon();
    }

    private void OnApplicationPause(bool value)
    {
        paused = value;
        if (workers == null) return;
        foreach (ComboCrewWorkerView worker in workers)
            if (worker != null) worker.SetPaused(value);
    }
    private void OnDisable()
    {
        SceneLoader.SceneLeaving -= SceneLeaving;
        leaving = true;
        Abandon();
        DOTween.Kill(this);
        SetClipboardPulse(false);
        profileTween?.Kill();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartMatch);
        if (backButton != null) backButton.onClick.RemoveListener(BackToHub);
        if (closeProfileButton != null) closeProfileButton.onClick.RemoveListener(CloseProfile);
    }
}
