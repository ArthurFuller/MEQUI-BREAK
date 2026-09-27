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
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private TMP_Text profileTitle;
    [SerializeField] private TMP_Text profileDescription;
    [SerializeField] private Button closeProfileButton;
    [SerializeField, Min(1f)] private float matchSeconds = 70f;
    [SerializeField, Min(.1f)] private float spawnInterval = 6f;
    [SerializeField, Min(1f)] private float patienceSeconds = 42f;
    [SerializeField, Min(.1f)] private float stageSeconds = 4f;
    [SerializeField, Range(0f, 1f)] private float secondOrderSpeed = .5f;
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
    private int repairStation = -1;
    private Incident incident;
    private float incidentRemaining, patienceGrace, absenceWarningRemaining;
    private int absentWorker = -1;
    private int absencePendingWorker = -1;
    private int problemWorker = -1;
    private bool absenceTriggered;
    private Tween clipboardPulse;
    private Color clipboardColor;

    public bool CanMoveWorker(int index) => (preparing || playing) && !leaving && !settled
        && index >= 0 && index < workers.Length && workers[index] != null
        && index != absentWorker && !workers[index].Busy;
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
    }

    private void OnEnable() => SceneLoader.SceneLeaving += SceneLeaving;

    private void Start()
    {
        UnityEngine.Device.Screen.orientation = ScreenOrientation.Portrait;
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
        if (clipboardHighlight != null)
        {
            clipboardColor = clipboardHighlight.color;
            clipboardPulse = clipboardHighlight.DOColor(
                new Color(1f, .85f, .22f, clipboardColor.a), .55f)
                .SetLoops(-1, LoopType.Yoyo).SetTarget(this);
        }
        RefreshHud();
    }

    private bool Validate()
    {
        string error = sceneLoader == null ? "SceneLoader" : resultPopup == null ? "ResultPopup" :
            startButton == null ? "StartButton" : backButton == null ? "BackButton" :
            preparationPanel == null ? "PreparationPanel" :
            timerLabel == null ? "TimerLabel" : messageLabel == null ? "MessageLabel" :
            cleanTrayLabel == null || dirtyTrayLabel == null ? "contadores de bandejas" :
            clipboard == null || !clipboard.Configured ? "Clipboard" :
            stations == null || stations.Length != 5 ? "cinco estações" :
            workers == null || workers.Length < 2 ? "jogador + funcionários" :
            workerCards == null || workerCards.Length != workers.Length ? "cards dos funcionários" :
            cardAnchors == null || cardAnchors.Length != workers.Length ? "âncoras dos cards" :
            orderCards == null || orderCards.Length < 2 ? "cards dos pedidos" :
            orderAnchors == null || orderAnchors.Length != orderCards.Length ? "âncoras dos pedidos" :
            departureAnchor == null ? "saída dos pedidos" :
            PlayerManager.Instance == null || PlayerManager.Instance.Profile == null ? "perfil (entre pelo Boot)" :
            PointsService.Instance == null || EventLogger.Instance == null ? "serviços do Boot" : null;
        if (error == null)
        {
            for (int i = 0; i < stations.Length; i++)
                if (stations[i] == null || !stations[i].Configured) { error = $"station[{i}]"; break; }
        }
        if (error == null)
        {
            for (int i = 0; i < workers.Length; i++)
                if (workers[i] == null || !workers[i].Configured || cardAnchors[i] == null
                    || workerCards[i] == null || !workerCards[i].Configured)
                { error = $"worker[{i}] e seu card"; break; }
        }
        if (error == null && (matchSeconds <= 0f
            || spawnInterval <= 0f || patienceSeconds <= 0f || stageSeconds <= 0f
            || initialTrays < 1 || washSecondsPerTray <= 0f)) error = "tempos ou bandejas iniciais";
        if (error == null)
        {
            for (int i = 0; i < orderCards.Length; i++)
                if (orderCards[i] == null || !orderCards[i].Configured || orderAnchors[i] == null)
                { error = $"order[{i}]"; break; }
        }
        if (error == null) return true;
        Debug.LogError("Combo Crew: configure " + error, this);
        if (startButton != null) startButton.interactable = false;
        return false;
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
            if (clipboard.IsOpen && clipboardPulse != null)
            {
                clipboardPulse.Kill();
                clipboardPulse = null;
                clipboardHighlight.color = clipboardColor;
            }
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
        clipboardPulse?.Kill();
        if (clipboardHighlight != null) clipboardHighlight.color = clipboardColor;
        preparationPanel.SetActive(false);
        matchRemaining = matchSeconds;
        nextSpawn = spawnInterval;
        EventLogger.Instance.BeginSession("combo_crew");
        logged = true;
        EventLogger.Instance.RecordUserAction("combo_start");
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
        int rank = Rank(id);
        float speed = (rank == 0 ? 1f : rank == 1 ? secondOrderSpeed : 0f)
            * workers[employee].SpeedAt(stage);
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
        AudioManager.Instance?.PlayConfirm();
        MequiHaptics.Selection();
        EventLogger.Instance?.RecordUserAction("combo_repair_station");
    }

    private void StartWashing()
    {
        if (repairStation == WashStation && (incident == Incident.Broken || incident == Incident.Repairing)) return;
        int employee = stationWorker[WashStation];
        if (employee < 0 || employee == absentWorker || workers[employee].Busy || moving[employee]) return;
        int batch = dirtyTrays;
        washingCount = batch;
        moving[employee] = true;
        workers[employee].Wash(stations[3].HandoffAnchor, stations[0].HandoffAnchor,
            batch, washSecondsPerTray / workers[employee].SpeedAt(WashStation), () =>
            {
                dirtyTrays -= batch;
                RefreshHud();
            }, () =>
            {
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
        workers[worker].Place(stations[station].WorkAnchor, false);
        if (preparing) RefreshHud();
        EventLogger.Instance?.RecordUserAction(worker == 0 ? "combo_player_move" : "combo_assign_worker");
        AudioManager.Instance?.PlayConfirm();
        MequiHaptics.Selection();
        HighlightStations(false);
        return true;
    }

    public void HighlightStations(bool visible)
    {
        if (stations == null || stationWorker == null) return;
        for (int i = 0; i < stations.Length; i++)
            stations[i].Highlight(visible && stationWorker[i] < 0);
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
            for (int i = 0; i < MainStationCount; i++) if (stationWorker[i] >= 0) staffed++;
            if (preparationHint != null)
                preparationHint.SetText("POSICIONE A EQUIPE  {0}/4", staffed);
            if (startButton != null) startButton.interactable = staffed == MainStationCount;
        }
        if (cleanTrayLabel != null) cleanTrayLabel.SetText("LIMPAS {0}", cleanTrays);
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
                        : dirtyTrays > 0 ? "Bandejas aguardando" : "Livre", false);
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
            if (incident == Incident.Broken) return "Toque para reparar";
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
        for (int i = 0; i < MainStationCount; i++) if (stationWorker[i] < 0) return false;
        return true;
    }

    public void OpenProfile(int index)
    {
        if (!preparing || profilePanel == null || index < 0 || index >= workers.Length) return;
        profileTitle.text = workers[index].DisplayName;
        profileDescription.text = workers[index].Description;
        profilePanel.SetActive(true);
    }

    public void CloseProfile()
    {
        if (profilePanel != null) profilePanel.SetActive(false);
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
        clipboardPulse?.Kill();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartMatch);
        if (backButton != null) backButton.onClick.RemoveListener(BackToHub);
        if (closeProfileButton != null) closeProfileButton.onClick.RemoveListener(CloseProfile);
    }
}
