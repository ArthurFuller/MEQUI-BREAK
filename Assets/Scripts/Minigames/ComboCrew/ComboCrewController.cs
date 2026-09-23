using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ComboCrewController : MonoBehaviour
{
    private enum OrderStatus { Hidden, Cooking, DeliveredWaitingWash, Completed, Missed }

    private sealed class RuntimeOrder
    {
        public OrderStatus Status;
        public int Tray = -1;
        public int View = -1;
        public float Deadline;
        public float Remaining;
    }

    [Header("Sessão e equipe")]
    [SerializeField] private MinigameSessionController session;
    [SerializeField] private CrewMember[] crewMembers;
    [SerializeField] private KitchenStation[] stations;

    [Header("Pedidos e bandejas — objetos da Hierarchy")]
    [SerializeField] private ComboOrderView[] orderViews;
    [SerializeField] private RectTransform[] trays;
    [SerializeField] private RectTransform[] cleanTrayAnchors;
    [SerializeField] private TMP_Text cleanTrayLabel;
    [SerializeField] private TMP_Text feedback;

    [Header("Balanceamento inicial")]
    [SerializeField, Min(.1f)] private float spawnInterval = 1.4f;
    [Tooltip("Prazo dos pedidos nos turnos 1, 2 e 3.")]
    [SerializeField] private Vector3 deadlinesByTurn = new Vector3(34f, 31f, 28f);
    [SerializeField, Min(.05f)] private float trayMoveSeconds = .22f;

    private RuntimeOrder[] orders;
    private bool[] trayClean;
    private Tween[] trayTweens;
    private int turn;
    private int targetOrders;
    private int nextOrder;
    private float spawnClock;
    private float feedbackClock;
    private float nextNoTrayFeedbackTime;
    private bool configured;

    public CrewMember[] CrewMembers => crewMembers;
    public KitchenStation[] Stations => stations;

    private void Awake()
    {
        configured = ValidateSetup();
        if (session != null) session.RegisterGameplay(configured);
        if (!configured) { enabled = false; return; }

        orders = new RuntimeOrder[session.MaximumOrders];
        for (int i = 0; i < orders.Length; i++) orders[i] = new RuntimeOrder();
        trayClean = new bool[trays.Length];
        trayTweens = new Tween[trays.Length];

        for (int i = 0; i < stations.Length; i++) stations[i].Initialize(this);
        for (int i = 0; i < orderViews.Length; i++) orderViews[i].Initialize();
        for (int i = 0; i < crewMembers.Length; i++) crewMembers[i].Initialize(this, i);

        for (int i = 0; i < crewMembers.Length; i++)
        {
            KitchenStation start = crewMembers[i].StartStation;
            if (start != null && start.AssignedCrew == null)
                AssignCrewInternal(crewMembers[i], start, true, false);
        }

        ResetTrays(true);
        feedback.text = string.Empty;
    }

    private void OnEnable()
    {
        if (!configured) return;
        session.TurnPreparing += Prepare;
        session.TurnStarted += Begin;
    }

    private void OnDisable()
    {
        if (session != null)
        {
            session.TurnPreparing -= Prepare;
            session.TurnStarted -= Begin;
        }
        if (trayTweens != null)
            foreach (Tween tween in trayTweens) tween?.Kill();
    }

    private void Prepare(int index, int count)
    {
        turn = index;
        targetOrders = count;
        nextOrder = 0;
        spawnClock = 0f;
        feedbackClock = 0f;
        nextNoTrayFeedbackTime = 0f;
        feedback.text = "Distribua a equipe antes do início do turno.";

        foreach (KitchenStation station in stations)
        {
            station.ClearWork();
            station.ForceRepair();
        }

        foreach (RuntimeOrder order in orders)
        {
            order.Status = OrderStatus.Hidden;
            order.Tray = -1;
            order.View = -1;
            order.Deadline = 0f;
            order.Remaining = 0f;
        }

        foreach (ComboOrderView view in orderViews) view.Hide(true);
        foreach (CrewMember member in crewMembers)
            if (member.IsAbsent) member.SetAbsent(false);
        ResetTrays(false);
    }

    private void Begin(int index, int count)
    {
        spawnClock = 0f;
        ShowFeedback("Turno iniciado. Mantenha o fluxo andando.");
    }

    private void Update()
    {
        if (!configured || !session.IsPlaying) return;

        float delta = Time.deltaTime;
        TickOrders(delta);
        if (!session.IsPlaying) return;

        foreach (KitchenStation station in stations)
        {
            if (station.Tick(delta)) HandleStationComplete(station);
            if (!session.IsPlaying) return;
        }

        spawnClock -= delta;
        if (nextOrder < targetOrders && spawnClock <= 0f)
        {
            if (TrySpawnOrder()) spawnClock = spawnInterval;
            else spawnClock = .25f;
        }

        if (feedbackClock > 0f && (feedbackClock -= delta) <= 0f)
            feedback.text = string.Empty;
    }

    public bool CanArrangeCrew(CrewMember member)
    {
        return configured && member != null && !member.IsAbsent && session.CanArrange;
    }

    public void AssignCrew(CrewMember member, KitchenStation target)
    {
        if (!CanArrangeCrew(member) || target == null) return;
        AssignCrewInternal(member, target, false, true);
    }

    private void AssignCrewInternal(CrewMember member, KitchenStation target, bool instant, bool record)
    {
        KitchenStation source = member.Station;
        if (source == target)
        {
            member.SetStationFromController(target, instant);
            return;
        }

        CrewMember occupant = target.AssignedCrew;
        if (source != null) source.AssignCrew(null);

        if (occupant != null && occupant != member)
        {
            target.AssignCrew(null);
            if (source != null)
            {
                source.AssignCrew(occupant);
                occupant.SetStationFromController(source, instant);
            }
            else
            {
                occupant.SetStationFromController(null, instant);
            }
        }

        target.AssignCrew(member);
        member.SetStationFromController(target, instant);

        if (!record) return;
        EventLogger.Instance?.RecordUserAction($"crew_{member.MemberIndex}_{target.Type.ToString().ToLowerInvariant()}");
        AudioManager.Instance?.PlayConfirm();
        MequiHaptics.Selection();

        if (target.Busy)
            ShowFeedback($"{target.ShortName}: trabalho retomado.", 1.2f);
        else if (source != null && source.Busy && source.AssignedCrew == null)
            ShowFeedback($"{source.ShortName}: aguardando alguém da equipe.", 1.2f);
        else if (member.WorkMultiplier(target.Type) > 1f)
            ShowFeedback($"Especialidade ativa em {target.ShortName}.", 1.2f);
    }

    public void HighlightStations(CrewMember member, bool value)
    {
        if (!configured) return;
        foreach (KitchenStation station in stations)
            station.Highlight(value && member != null && !member.IsAbsent && station != member.Station);
    }

    public void RemoveCrewForAbsence(CrewMember member)
    {
        if (member == null) return;
        KitchenStation station = member.Station;
        if (station != null && station.AssignedCrew == member) station.AssignCrew(null);
        member.SetStationFromController(null, false);
    }

    public void RestoreCrewAfterAbsence(CrewMember member, KitchenStation preferred)
    {
        if (member == null) return;
        if (preferred != null && preferred.AssignedCrew == null)
            AssignCrewInternal(member, preferred, false, false);
        else
            member.SetStationFromController(null, false);
    }

    public void StationRepaired(KitchenStation station)
    {
        ShowFeedback($"{station.ShortName}: equipamento reparado.");
        EventLogger.Instance?.RecordActivityEvent($"incident_repaired_{station.Type.ToString().ToLowerInvariant()}");
        AudioManager.Instance?.PlayConfirm();
        MequiHaptics.Confirm();
    }

    public void ShowFeedback(string message, float seconds = 1.8f)
    {
        if (feedback == null) return;
        feedback.text = message;
        feedbackClock = Mathf.Max(.1f, seconds);
    }

    private void TickOrders(float delta)
    {
        for (int id = 0; id < targetOrders; id++)
        {
            RuntimeOrder order = orders[id];
            if (order.Status != OrderStatus.Cooking && order.Status != OrderStatus.DeliveredWaitingWash)
                continue;

            int stationIndex = FindStationIndex(id);
            string state = stationIndex >= 0 ? stations[stationIndex].ShortName : "AGUARDANDO";
            float stageProgress = stationIndex >= 0 ? stations[stationIndex].Progress : 0f;

            if (order.Status == OrderStatus.Cooking)
            {
                order.Remaining = Mathf.Max(0f, order.Remaining - delta);
                if (order.View >= 0)
                    orderViews[order.View].Refresh(order.Remaining, order.Deadline, state, stageProgress);

                if (order.Remaining <= 0f)
                {
                    ExpireOrder(id);
                    if (!session.IsPlaying) return;
                }
            }
            else if (order.View >= 0)
            {
                // Depois da entrega o prazo não conta mais, mas a etapa de louça continua
                // exibindo progresso para o jogador entender que o fluxo ainda não terminou.
                orderViews[order.View].RefreshStage(state, stageProgress);
            }
        }
    }

    private bool TrySpawnOrder()
    {
        KitchenStation prep = stations[0];
        if (prep.Busy) return false;

        int tray = FindCleanTray();
        int view = FindFreeView();
        if (tray < 0 || view < 0)
        {
            if (tray < 0 && Time.unscaledTime >= nextNoTrayFeedbackTime)
            {
                nextNoTrayFeedbackTime = Time.unscaledTime + 1.5f;
                ShowFeedback("Sem bandeja limpa. Priorize a louça.");
                EventLogger.Instance?.RecordActivityEvent("combo_no_clean_tray");
            }
            return false;
        }

        int id = nextOrder++;
        float deadline = DeadlineForTurn(turn);
        RuntimeOrder order = orders[id];
        order.Status = OrderStatus.Cooking;
        order.Tray = tray;
        order.View = view;
        order.Deadline = deadline;
        order.Remaining = deadline;
        trayClean[tray] = false;

        orderViews[view].Bind(id, deadline);
        prep.TryLoad(id, tray);
        MoveTray(tray, prep.TrayAnchor, false);
        RefreshTrayLabel();
        AudioManager.Instance?.PlayConfirm();
        return true;
    }

    private void HandleStationComplete(KitchenStation station)
    {
        int id = station.OrderId;
        int tray = station.TrayId;
        if (id < 0 || id >= targetOrders || tray < 0) return;
        RuntimeOrder order = orders[id];

        if (station.Type == KitchenStation.StationType.Wash)
        {
            station.ClearWork();
            order.Status = OrderStatus.Completed;
            ReleaseTray(tray);
            if (order.View >= 0) orderViews[order.View].Hide(false);
            AudioManager.Instance?.PlayConfirm();
            MequiHaptics.Confirm();
            session.TryResolveOrder(turn, id, true);
            return;
        }

        int current = System.Array.IndexOf(stations, station);
        if (current < 0 || current + 1 >= stations.Length) return;

        if (station.Type == KitchenStation.StationType.Delivery
            && order.Status == OrderStatus.Cooking)
        {
            order.Status = OrderStatus.DeliveredWaitingWash;
            if (order.View >= 0) orderViews[order.View].MarkDelivered();
            ShowFeedback("Pedido entregue. Agora libere a bandeja na louça.");
            EventLogger.Instance?.RecordActivityEvent($"turn_{turn + 1}_order_{id}_delivered_to_counter");
            AudioManager.Instance?.PlayReady();
        }

        KitchenStation next = stations[current + 1];
        if (next.Busy) return;

        station.ClearWork();
        next.TryLoad(id, tray);
        MoveTray(tray, next.TrayAnchor, false);

        if (order.View >= 0)
        {
            if (order.Status == OrderStatus.Cooking)
                orderViews[order.View].Refresh(order.Remaining, order.Deadline, next.ShortName, next.Progress);
            else
                orderViews[order.View].RefreshStage(next.ShortName, next.Progress);
        }
    }

    private void ExpireOrder(int id)
    {
        RuntimeOrder order = orders[id];
        if (order.Status != OrderStatus.Cooking) return;

        int stationIndex = FindStationIndex(id);
        if (stationIndex >= 0) stations[stationIndex].ClearWork();
        order.Status = OrderStatus.Missed;
        if (order.View >= 0) orderViews[order.View].MarkExpired();
        ReleaseTray(order.Tray);
        ShowFeedback("O pedido expirou. A bandeja voltou ao estoque limpo.");
        AudioManager.Instance?.PlayError();
        MequiHaptics.Reject();
        session.TryResolveOrder(turn, id, false);
    }

    private int FindStationIndex(int orderId)
    {
        for (int i = 0; i < stations.Length; i++)
            if (stations[i].Busy && stations[i].OrderId == orderId) return i;
        return -1;
    }

    private int FindCleanTray()
    {
        for (int i = 0; i < trayClean.Length; i++) if (trayClean[i]) return i;
        return -1;
    }

    private int FindFreeView()
    {
        for (int i = 0; i < orderViews.Length; i++) if (orderViews[i].Available) return i;
        return -1;
    }

    private float DeadlineForTurn(int index)
    {
        return Mathf.Max(1f, index <= 0 ? deadlinesByTurn.x : index == 1 ? deadlinesByTurn.y : deadlinesByTurn.z);
    }

    private void ResetTrays(bool instant)
    {
        for (int i = 0; i < trays.Length; i++)
        {
            trayClean[i] = true;
            MoveTray(i, cleanTrayAnchors[i], instant);
        }
        RefreshTrayLabel();
    }

    private void ReleaseTray(int tray)
    {
        if (tray < 0 || tray >= trays.Length) return;
        trayClean[tray] = true;
        MoveTray(tray, cleanTrayAnchors[tray], false);
        RefreshTrayLabel();
    }

    private void MoveTray(int index, RectTransform anchor, bool instant)
    {
        if (index < 0 || index >= trays.Length || anchor == null) return;
        trayTweens[index]?.Kill();
        if (instant) trays[index].position = anchor.position;
        else trayTweens[index] = trays[index].DOMove(anchor.position, trayMoveSeconds).SetEase(Ease.OutCubic);
    }

    private void RefreshTrayLabel()
    {
        int clean = 0;
        if (trayClean != null) foreach (bool value in trayClean) if (value) clean++;
        cleanTrayLabel.SetText("Bandejas limpas: {0}/4", clean);
    }

    private bool SetupError(string detail)
    {
        Debug.LogError($"Combo Crew — {name}: {detail}", this);
        return false;
    }

    private bool ValidateSetup()
    {
        if (session == null) return SetupError("session não atribuído.");
        if (session.MaximumOrders < 1) return SetupError("ordersPerTurn precisa conter três inteiros positivos.");
        if (crewMembers == null || crewMembers.Length != 4) return SetupError("crewMembers precisa de quatro elementos (jogador + três funcionários).");
        if (stations == null || stations.Length != 5) return SetupError("stations precisa de cinco elementos na ordem preparo, chapa, montagem, entrega e louça.");
        if (orderViews == null || orderViews.Length != 4) return SetupError("orderViews precisa de quatro elementos reutilizáveis.");
        if (trays == null || trays.Length != 4) return SetupError("trays precisa de quatro bandejas.");
        if (cleanTrayAnchors == null || cleanTrayAnchors.Length != 4) return SetupError("cleanTrayAnchors precisa de quatro posições.");
        if (cleanTrayLabel == null) return SetupError("cleanTrayLabel não atribuído.");
        if (feedback == null) return SetupError("feedback não atribuído.");
        if (spawnInterval <= 0f || trayMoveSeconds <= 0f || deadlinesByTurn.x <= 0f || deadlinesByTurn.y <= 0f || deadlinesByTurn.z <= 0f)
            return SetupError("Tempos de spawn, prazo ou movimento inválidos.");

        KitchenStation.StationType[] expected = {
            KitchenStation.StationType.Preparation,
            KitchenStation.StationType.Grill,
            KitchenStation.StationType.Assembly,
            KitchenStation.StationType.Delivery,
            KitchenStation.StationType.Wash
        };

        for (int i = 0; i < stations.Length; i++)
        {
            if (stations[i] == null) return SetupError($"stations[{i}] não atribuído.");
            if (!stations[i].Configured) return SetupError($"stations[{i}] ({stations[i].name}): {stations[i].ConfigurationError}.");
            if (stations[i].Type != expected[i]) return SetupError($"stations[{i}] precisa ser {expected[i]}.");
        }

        for (int i = 0; i < crewMembers.Length; i++)
        {
            if (crewMembers[i] == null) return SetupError($"crewMembers[{i}] não atribuído.");
            if (!crewMembers[i].Configured) return SetupError($"crewMembers[{i}] ({crewMembers[i].name}): {crewMembers[i].ConfigurationError}.");
        }

        for (int i = 0; i < orderViews.Length; i++)
        {
            if (orderViews[i] == null) return SetupError($"orderViews[{i}] não atribuído.");
            if (!orderViews[i].Configured) return SetupError($"orderViews[{i}] ({orderViews[i].name}): {orderViews[i].ConfigurationError}.");
            if (trays[i] == null) return SetupError($"trays[{i}] não atribuído.");
            if (cleanTrayAnchors[i] == null) return SetupError($"cleanTrayAnchors[{i}] não atribuído.");
        }
        return true;
    }
}
