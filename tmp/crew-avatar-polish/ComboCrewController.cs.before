using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Pedidos, funcionários e bandejas do Combo Crew; todos os visuais são da Hierarchy.</summary>
[DisallowMultipleComponent]
public sealed partial class ComboCrewController : MonoBehaviour
{
    private const int MainStationCount = 4;
    private const int WashStation = 4;
    private static readonly string[] StationNames = { "PREPARO", "CHAPA", "MONTAGEM", "ENTREGA", "LAVA-LOUÇA" };
    private enum OrderState { Free, Working, Travelling, Resolving }
    private enum Incident { None, Warning, Broken, Repairing }
    private sealed class Order
    {
        public OrderState State;
        public int Sequence, Stage, Items;
        public readonly Sprite[] MenuItems = new Sprite[3];
        public float WorkMultiplier;
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
    [SerializeField] private Sprite profileCellSprite;
    [SerializeField] private TMP_Text profileTitle;
    [SerializeField] private TMP_Text profileDescription;
    [SerializeField] private Button closeProfileButton;
    [SerializeField, Min(1f)] private float matchSeconds = 70f;
    [SerializeField, Min(.1f)] private float spawnInterval = 7f;
    [SerializeField, Min(1f)] private float patienceSeconds = 42f;
    [SerializeField, Min(.1f)] private float stageSeconds = 4f;
    [SerializeField, Min(1)] private int initialTrays = 4;
    [SerializeField, Min(.1f)] private float washSecondsPerTray = 6f;
    [SerializeField, Min(0)] private int pointsPerDelivery = 4;
    [SerializeField, Min(0f)] private float repairWarningSeconds = 3f;
    [Header("Variação do turno")]
    [SerializeField] private ComboCrewTurnProfile[] turnProfiles;
    [SerializeField, Tooltip("Zero cria uma semente nova; outro valor repete o mesmo sorteio.")]
    private int seedOverride;
    [SerializeField] private Sprite[] menuSprites;

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
    private float[] workerWaitStarted;
    private float matchRemaining, nextSpawn, messageRemaining;
    private int nextSequence, delivered, missed, cleanTrays, dirtyTrays;
    private bool preparing, playing, closing, settled, leaving, paused, logged;
    private int washingCount;
    private int activeDeliveries;
    private bool washCounterReserved;
    private bool breakReorganizationLogged;
    private Tween clipboardPulse;
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
    private Image profilePortraitBody, profilePortraitFace, profilePortraitHat;
    private ComboCrewTurnIntro turnIntro;
    private bool[] generatedProfiles;

    public bool CanMoveWorker(int index) => (preparing || playing) && !leaving && !settled
        && index >= 0 && index < workers.Length && workers[index] != null
        && workerAvailable != null && !IsUnavailable(index) && !workers[index].Busy;
    public bool CanNudgeWorkerCards => preparing && clipboard != null && clipboard.IsOpen
        && (profilePanel == null || !profilePanel.activeSelf);
    public RectTransform CardHome(int index) => cardAnchors != null && index >= 0
        && index < cardAnchors.Length ? cardAnchors[index] : null;
    public string WorkerTrait(int index) => workers != null && index >= 0
        && index < workers.Length ? workers[index].CardSummary() : string.Empty;
    public ComboCrewWorkerView.CardBadge[] WorkerBadges(int index) => workers != null && index >= 0
        && index < workers.Length ? workers[index].CardBadges() : System.Array.Empty<ComboCrewWorkerView.CardBadge>();
    public string WorkerName(int index) => workers != null && index >= 0
        && index < workers.Length ? workers[index].DisplayName : string.Empty;
    public void CopyWorkerPortrait(int index, Image body, Image face, Image hat)
    {
        if (workers != null && index >= 0 && index < workers.Length && workers[index] != null)
            workers[index].CopyPortrait(body, face, hat);
    }

    private void Awake()
    {
        HideLegacyTutorialOverlay();
        if (startButton != null) startButton.onClick.AddListener(StartMatch);
        if (backButton != null) backButton.onClick.AddListener(BackToHub);
        if (closeProfileButton != null) closeProfileButton.onClick.AddListener(CloseProfile);
        if (messageLabel != null) messageLabel.gameObject.SetActive(false);
        if (dirtyTrayLabel != null) dirtyTrayLabel.gameObject.SetActive(false);
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

    private void HideLegacyTutorialOverlay()
    {
        // Uma cópia temporária da cena aberta no Editor pode conservar o antigo
        // overlay mesmo depois de ele ter sido removido do arquivo da cena.
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "TutorialRoot_ComboCrew")
                    child.gameObject.SetActive(false);
            }
        }
    }

    private void OnEnable() => SceneLoader.SceneLeaving += SceneLeaving;

    private void Start()
    {
        UnityEngine.Device.Screen.orientation = ScreenOrientation.Portrait;
        Canvas.ForceUpdateCanvases();
        if (profileRect != null) profileHomePosition = profileRect.position;
        EnsureTurnProfiles();
        EnsureRoster();
        if (!Validate()) { ShowMessage("Configuração incompleta. Consulte o Console."); return; }
        orders = new Order[orderCards.Length];
        for (int i = 0; i < orders.Length; i++)
        {
            orders[i] = new Order();
            orderCards[i].gameObject.SetActive(false);
        }
        assigned = new int[workers.Length];
        moving = new bool[workers.Length];
        workerWaitStarted = new float[workers.Length];
        for (int i = 0; i < workerWaitStarted.Length; i++) workerWaitStarted[i] = -1f;
        stationWorker = new int[stations.Length];
        for (int i = 0; i < workers.Length; i++) assigned[i] = -1;
        for (int i = 0; i < stations.Length; i++)
        {
            stationWorker[i] = -1;
            stations[i].Bind(this, i);
        }
        cleanTrays = initialTrays;
        workers[0].ApplyPlayerAppearance();
        PrepareTurn();
        for (int i = 0; i < workerCards.Length; i++)
            workerCards[i].RefreshPortrait();
        preparing = true;
        CacheClipboardHandle();
        RefreshHud();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            turnIntro = gameObject.AddComponent<ComboCrewTurnIntro>();
            turnIntro.Build(canvas.rootCanvas, timerLabel);
            turnIntro.Show(turnProfile.title);
        }
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
            && turnProfiles != null && turnProfiles.Length == 3
            && PlayerManager.Instance != null && PlayerManager.Instance.Profile != null
            && PointsService.Instance != null && EventLogger.Instance != null;
        if (valid)
        {
            int totalWeight = 0;
            for (int i = 0; i < turnProfiles.Length; i++)
                if (turnProfiles[i] != null)
                    totalWeight += Mathf.Max(0, turnProfiles[i].selectionWeight);
            valid = totalWeight > 0;
        }
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
        if (!valid)
        {
            Debug.LogError("Combo Crew: " + ConfigurationError(), this);
            if (startButton != null) startButton.interactable = false;
        }
        return valid;
    }

    private string ConfigurationError()
    {
        if (sceneLoader == null) return "SceneLoader ausente.";
        if (resultPopup == null) return "ResultPopup ausente.";
        if (startButton == null || backButton == null || preparationPanel == null)
            return "Botões ou painel de preparação ausentes.";
        if (timerLabel == null || messageLabel == null || cleanTrayLabel == null
            || dirtyTrayLabel == null) return "Textos do HUD ausentes.";
        if (clipboard == null || !clipboard.Configured) return "Prancheta incompleta.";
        if (stations == null || stations.Length != 5) return "Lista de estações inválida.";
        for (int i = 0; i < stations.Length; i++)
            if (stations[i] == null || !stations[i].Configured) return $"Estação {i} incompleta.";
        if (workers == null || workers.Length < 2 || workerCards == null
            || workerCards.Length != workers.Length || cardAnchors == null
            || cardAnchors.Length != workers.Length) return "Listas de personagens inválidas.";
        for (int i = 0; i < workers.Length; i++)
            if (workers[i] == null || !workers[i].Configured || workerCards[i] == null
                || !workerCards[i].Configured || cardAnchors[i] == null)
                return $"Personagem ou card {i} incompleto.";
        if (orderCards == null || orderCards.Length < 2 || orderAnchors == null
            || orderAnchors.Length != orderCards.Length || departureAnchor == null)
            return "Lista de pedidos ou destino de entrega inválido.";
        for (int i = 0; i < orderCards.Length; i++)
            if (orderCards[i] == null || !orderCards[i].Configured || orderAnchors[i] == null)
                return $"Card de pedido {i} incompleto.";
        if (turnProfiles == null || turnProfiles.Length != 3) return "Perfis de turno ausentes.";
        int totalWeight = 0;
        for (int i = 0; i < turnProfiles.Length; i++)
            if (turnProfiles[i] != null) totalWeight += Mathf.Max(0, turnProfiles[i].selectionWeight);
        if (totalWeight <= 0) return "Nenhum perfil de turno está habilitado.";
        if (PlayerManager.Instance == null || PlayerManager.Instance.Profile == null)
            return "Perfil do jogador ainda não carregado.";
        if (PointsService.Instance == null || EventLogger.Instance == null)
            return "Serviços de pontos ou eventos ausentes.";
        return "Parâmetros de tempo ou bandejas inválidos.";
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
            if (nextSpawn <= 0f && matchRemaining > 0f)
            {
                TrySpawn();
                nextSpawn = spawnInterval;
            }
            if (matchRemaining <= 0f)
            {
                closing = true;
                EventLogger.Instance?.MarkTimeLimitReached();
                ShowMessage("Finalizando pedidos", 3f);
            }
        }
        UpdateEvents(delta);
        for (int i = 0; i < orders.Length; i++)
        {
            Order order = orders[i];
            if (order.State == OrderState.Free || order.State == OrderState.Resolving) continue;
            // A entrega já saiu da bancada: o pedido não pode expirar durante a rota.
            if (!(order.State == OrderState.Travelling && order.Stage == 3))
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
        if (!preparing || orders == null || leaving || !crewFeasible || !ReadyToStart()) return;
        preparing = false;
        playing = true;
        activeDeliveries = 0;
        if (clipboard.IsOpen) clipboard.Toggle();
        SetClipboardPulse(false);
        preparationPanel.SetActive(false);
        startButton.gameObject.SetActive(false);
        foreach (var station in stations) station.SetProgressVisible(true);
        matchRemaining = matchSeconds;
        nextSpawn = spawnInterval;
        EventLogger.Instance.BeginSession("combo_crew");
        logged = true;
        EventLogger.Instance.RecordActivityEvent($"combo_turn_{turnProfile.name}_{turnSeed}");
        EventLogger.Instance.RecordUserAction("combo_start");
        breakReorganizationLogged = false;
        int specialistPlacements = 0;
        int specialists = 0;
        for (int employee = 0; employee < workers.Length; employee++)
        {
            float best = 1f;
            for (int stage = 0; stage < stations.Length; stage++) best = Mathf.Max(best, workers[employee].SpeedAt(stage));
            if (!workerAvailable[employee] || best <= 1f) continue;
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
            int size = RollOrderSize();
            order.Items = (1 << size) - 1;
            order.WorkMultiplier = size == 1 ? turnProfile.orderTimeMultipliers.x
                : size == 2 ? turnProfile.orderTimeMultipliers.y
                : turnProfile.orderTimeMultipliers.z;
            for (int item = 0; item < size; item++)
            {
                if (menuSprites == null || menuSprites.Length == 0)
                {
                    order.MenuItems[item] = null;
                    continue;
                }
                int pick, attempts = 0;
                do
                {
                    pick = turnRandom.Next(menuSprites.Length);
                } while (menuSprites.Length >= size && attempts++ < menuSprites.Length
                    && AlreadyChosen(order.MenuItems, item, menuSprites[pick]));
                order.MenuItems[item] = menuSprites[pick];
            }
            order.Patience = patienceSeconds;
            order.Remaining = order.Patience;
            order.Progress = 0f;
            order.ReadyHold = 0f;
            order.HasTray = false;
            orderCards[i].Show(order.Items, orderAnchors[Rank(i)], order.MenuItems);
            EventLogger.Instance?.RecordActivityEvent("combo_order_created");
            return true;
        }
        EventLogger.Instance?.RecordActivityEvent("combo_order_skipped_full");
        return false;
    }

    private static bool AlreadyChosen(Sprite[] chosen, int count, Sprite candidate)
    {
        for (int i = 0; i < count; i++) if (chosen[i] == candidate) return true;
        return false;
    }

    private void Advance(int id, float delta)
    {
        Order order = orders[id];
        int stage = order.Stage;
        int employee = stationWorker[stage];
        if (employee < 0 || IsUnavailable(employee) || moving[employee] || workers[employee].Busy
            || IsBlocked(stage) || (stage == MainStationCount - 1 && washCounterReserved)
            || FirstAtStage(stage) != id) return;
        float speed = workers[employee].SpeedAt(stage);
        if (speed <= 0f) return;
        if (order.Progress < 1f)
            order.Progress = Mathf.Min(1f, order.Progress + delta * speed
                / (stageSeconds * Mathf.Max(.1f, order.WorkMultiplier)));
        if (order.Progress >= 1f) order.ReadyHold += delta;
        if (order.Progress < 1f || order.ReadyHold < .18f || (stage == 0 && cleanTrays < 1)) return;
        if (stage < MainStationCount - 1 && StageOccupied(stage + 1, id)) return;
        if (stage == 0) cleanTrays--;
        order.HasTray = true;
        order.State = OrderState.Travelling;
        moving[employee] = true;
        if (stage == MainStationCount - 1)
        {
            activeDeliveries++;
            bool deliveredAtDestination = false;
            workers[employee].SetTrayItems(order.Items, order.MenuItems);
            workers[employee].Deliver(departureAnchor, stations[stage].ForegroundLayer, () =>
            {
                if (!playing || order.State != OrderState.Travelling) return;
                deliveredAtDestination = true;
                Resolve(id, true, false);
            }, () =>
            {
                moving[employee] = false;
                activeDeliveries = Mathf.Max(0, activeDeliveries - 1);
                if (!deliveredAtDestination) return;
                workers[employee].SetTrayItems(0);
                dirtyTrays++;
                StartWashing();
                RefreshHud();
                TryFinish();
            });
            return;
        }
        workers[employee].Travel(stations[stage + 1].HandoffAnchor,
            stations[stage + 1].ForegroundLayer, () =>
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

    private void StartWashing()
    {
        if (washingCount > 0 || dirtyTrays < 1 || IsBlocked(WashStation)) return;
        // dirtyTrays só inclui bandejas que já voltaram de uma entrega ou pedido encerrado.
        // A reserva segura o balcão apenas até a coleta, sem exigir uma pausa entre pedidos.
        int employee = stationWorker[WashStation];
        if (employee < 0 || IsUnavailable(employee) || workers[employee].Busy || moving[employee]) return;
        int batch = dirtyTrays;
        washingCount = batch;
        stations[WashStation].ResetProgress();
        washCounterReserved = true;
        moving[employee] = true;
        if (!workers[employee].Wash(stations[3].HandoffAnchor, stations[0].HandoffAnchor,
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
                stations[WashStation].ResetProgress();
                AudioManager.Instance?.PlayReady();
                RefreshHud();
                TryFinish();
            }))
        {
            washingCount = 0;
            washCounterReserved = false;
            moving[employee] = false;
        }
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
            || IsUnavailable(worker) || station < 0 || station >= stations.Length
            || workers[worker].Busy || moving[worker]) return false;
        int old = assigned[worker];
        if (old == station) return true;
        int occupant = stationWorker[station];
        if (occupant >= 0 && occupant != worker)
        {
            bool occupantAway = workerAway[occupant];
            if (!occupantAway && (old < 0 || workers[occupant].Busy || moving[occupant]))
            { ShowMessage("Esta bancada já tem alguém."); return false; }
            assigned[occupant] = old;
            if (old >= 0) workers[occupant].Place(stations[old].WorkAnchor, false);
            else workers[occupant].ReturnToReserveAfterBreak();
        }
        if (old >= 0) stationWorker[old] = occupant;
        assigned[worker] = station;
        stationWorker[station] = worker;
        clipboardGuidanceComplete = true;
        SetClipboardPulse(false);
        workers[worker].Place(stations[station].WorkAnchor, false);
        workerCards[worker].SetAssigned(true);
        if (occupant >= 0 && occupant != worker)
            workerCards[occupant].SetAssigned(old >= 0);
        if (preparing) RefreshHud();
        EventLogger.Instance?.RecordUserAction(worker == 0 ? "combo_player_move" : "combo_assign_worker");
        if (playing && !breakReorganizationLogged && AnyBreakActive())
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
            stations[i].Highlight(visible && CanReceiveWorker(draggedWorker, i));
        }
    }

    private bool CanReceiveWorker(int worker, int station)
    {
        if (worker < 0 || worker >= workers.Length || station < 0
            || station >= stationWorker.Length || IsUnavailable(worker)
            || workers[worker].Busy || moving[worker]) return false;
        int occupant = stationWorker[station];
        if (occupant < 0 || occupant == worker || workerAway[occupant]) return true;
        return assigned[worker] >= 0 && !IsUnavailable(occupant)
            && !workers[occupant].Busy && !moving[occupant];
    }

    private int DropTargetFor(int worker, RectTransform visual)
    {
        if (visual == null || stations == null || stationWorker == null) return -1;
        for (int i = 0; i < stations.Length; i++)
            if (CanReceiveWorker(worker, i) && stations[i].ContainsDraggedVisual(visual))
                return i;
        return -1;
    }

    public int UpdateDropTarget(int worker, RectTransform visual)
    {
        int target = DropTargetFor(worker, visual);
        if (stations != null)
            for (int i = 0; i < stations.Length; i++) stations[i].SetDropHover(i == target);
        return target;
    }

    public bool AssignDraggedWorker(int worker, RectTransform visual)
    {
        int target = DropTargetFor(worker, visual);
        return target >= 0 && AssignWorker(worker, target);
    }

    private void TryFinish()
    {
        if (!closing || settled) return;
        if (activeDeliveries > 0) return;
        for (int i = 0; i < orders.Length; i++) if (orders[i].State != OrderState.Free) return;
        settled = true;
        playing = false;
        backButton.interactable = false;
        int points = PointsService.Instance.AwardParticipation(pointsPerDelivery * delivered);
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
            if (preparing) timerLabel.text = string.Empty;
            else if (playing)
            {
                int seconds = Mathf.CeilToInt(matchRemaining);
                timerLabel.SetText("{0}:{1:00}", seconds / 60, seconds % 60);
            }
            else timerLabel.text = string.Empty;
        }
        if (preparing)
        {
            int staffed = 0;
            for (int i = 0; i < stationWorker.Length; i++) if (stationWorker[i] >= 0) staffed++;
            if (preparationHint != null)
                preparationHint.text = crewFeasible ? "POSICIONE AO MENOS 1 PERSONAGEM"
                    : "TURNO INVIÁVEL: REVISE OS PERFIS";
            if (startButton != null) startButton.interactable = staffed > 0 && crewFeasible;
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
                stations[stage].Refresh(stationIncidents[stage] == Incident.Repairing
                        ? 1f - stationTimers[stage] / stationRepairDurations[stage]
                        : stationIncidents[stage] == Incident.Broken ? 0f
                        : washingCount > 0 && stationWorker[stage] >= 0
                            ? workers[stationWorker[stage]].WashProgress : 0f,
                    stationIncidents[stage] != Incident.None ? StationStatus(stage, id)
                        : stationWorker[stage] < 0 ? "Sem funcionário"
                        : IsUnavailable(stationWorker[stage]) ? "Ausente"
                        : IsWarned(stationWorker[stage]) ? "Pausa em breve"
                        : washingCount > 0 ? "Lavando"
                        : dirtyTrays > 0 ? "Bandejas aguardando" : "Livre", false,
                    stationWorker[stage] >= 0 && workers[stationWorker[stage]].WashingAtSink);
            else
                stations[stage].Refresh(stationIncidents[stage] == Incident.Repairing
                        ? 1f - stationTimers[stage] / stationRepairDurations[stage]
                        : id < 0 ? 0f : orders[id].Progress,
                    StationStatus(stage, id),
                    (id >= 0 && orders[id].State == OrderState.Working
                        && (orders[id].HasTray || (stage == 0 && cleanTrays > 0)))
                    || (stage == 3 && dirtyTrays > 0));
        }
    }

    private void UpdateProblemWorker()
    {
        for (int worker = 0; worker < workers.Length; worker++)
        {
            int station = assigned[worker];
            bool present = playing && station >= 0 && !IsUnavailable(worker)
                && !moving[worker] && !workers[worker].Busy && !workers[worker].Dragging;
            bool broken = present && stationIncidents[station] == Incident.Broken;
            int order = present && station < MainStationCount - 1
                ? FirstAtStage(station) : -1;
            bool waiting = order >= 0 && stationIncidents[station] == Incident.None
                && orders[order].Progress >= 1f && StageOccupied(station + 1, order);
            if (waiting)
            {
                if (workerWaitStarted[worker] < 0f) workerWaitStarted[worker] = Time.time;
            }
            else workerWaitStarted[worker] = -1f;
            workers[worker].ShowStationProblem(broken);
            workers[worker].ShowFlowBlocked(waiting
                && Time.time - workerWaitStarted[worker] >= 2.5f);
        }
    }

    private string StationStatus(int stage, int id)
    {
        int employee = stationWorker[stage];
        if (stationIncidents[stage] == Incident.Warning) return "Precisa de ajuste";
        if (stationIncidents[stage] == Incident.Broken) return "Quebrada";
        if (stationIncidents[stage] == Incident.Repairing) return "Em manutenção";
        if (employee < 0) return "Sem funcionário";
        if (IsUnavailable(employee)) return "Ausente";
        if (IsWarned(employee)) return "Pausa em breve";
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
        if (clipboardHandle == null) return;
        if (enabled && (clipboardPulse == null || !clipboardPulse.IsActive()))
        {
            Sequence wave = DOTween.Sequence().SetTarget(this);
            wave.Append(clipboardHandle.DOAnchorPosY(clipboardHandlePosition.y + 14f,
                clipboardPulseSeconds * .38f).SetEase(Ease.OutCubic));
            wave.Join(clipboardHandle.DOScale(clipboardHandleScale * 1.018f,
                clipboardPulseSeconds * .38f).SetEase(Ease.OutCubic));
            wave.Append(clipboardHandle.DOAnchorPos(clipboardHandlePosition,
                clipboardPulseSeconds * .62f).SetEase(Ease.OutBack));
            wave.Join(clipboardHandle.DOScale(clipboardHandleScale,
                clipboardPulseSeconds * .62f).SetEase(Ease.OutCubic));
            clipboardPulse = wave.AppendInterval(.12f).SetLoops(-1);
        }
        else if (!enabled && clipboardPulse != null)
        {
            clipboardPulse.Kill();
            clipboardPulse = null;
            clipboardHandle.anchoredPosition = clipboardHandlePosition;
            clipboardHandle.localScale = clipboardHandleScale;
        }
    }

    private void CacheClipboardHandle()
    {
        if (clipboardHighlight == null) return;
        RectTransform frame = clipboardHighlight.rectTransform;
        clipboardHandle = frame;
        clipboardHandlePosition = frame.anchoredPosition;
        clipboardHandleScale = frame.localScale;
    }

    public void OpenProfile(int index)
    {
        if ((!preparing && !playing) || profilePanel == null || index < 0 || index >= workers.Length) return;
        profileTween?.Kill();
        EnsureProfileLayout();
        CopyWorkerPortrait(index, profilePortraitBody, profilePortraitFace, profilePortraitHat);
        profileSource = workerCards != null && index < workerCards.Length && workerCards[index] != null
            ? workerCards[index].transform as RectTransform : null;
        profileTitle.text = workers[index].DisplayName;
        profileDescription.text = workers[index].ProfileDetails();
        profileDescription.richText = true;
        profileDescription.fontSize = 29f;
        profileDescription.enableAutoSizing = true;
        profileDescription.fontSizeMin = 24f;
        profileDescription.fontSizeMax = 29f;
        profileDescription.fontStyle = FontStyles.Bold;
        profileDescription.color = new Color32(239, 239, 234, 255);
        RectTransform detailsRect = profileDescription.rectTransform;
        detailsRect.anchoredPosition = new Vector2(126f, 28f);
        detailsRect.sizeDelta = new Vector2(438f, 246f);
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

    private void EnsureProfileLayout()
    {
        if (profileRect == null) return;
        Sprite cellSprite = profileCellSprite != null
            ? profileCellSprite : Resources.Load<Sprite>("ComboCrew/profile_cell");
        if (cellSprite == null) cellSprite = profileRect.GetComponent<Image>()?.sprite;
        Image details = ProfileImage("AreaDosDetalhes", profileRect,
            new Vector2(124f, 30f), new Vector2(510f, 290f), cellSprite,
            Color.white);
        details.transform.SetAsFirstSibling();
        Image portrait = ProfileImage("CartaoDoFuncionario", profileRect,
            new Vector2(-276f, 30f), new Vector2(245f, 290f), cellSprite,
            Color.white);
        portrait.transform.SetSiblingIndex(1);
        Transform oldAccent = portrait.transform.Find("AcentoDoCartao");
        if (oldAccent != null) oldAccent.gameObject.SetActive(false);
        profilePortraitBody = ProfileImage("CorpoFuncionario", portrait.rectTransform,
            new Vector2(0f, 19f), new Vector2(160f, 160f), null, Color.white);
        profilePortraitFace = ProfileImage("RostoFuncionario", portrait.rectTransform,
            new Vector2(0f, 19f), new Vector2(160f, 160f), null, Color.white);
        profilePortraitHat = ProfileImage("ChapeuFuncionario", portrait.rectTransform,
            new Vector2(0f, 66f), new Vector2(160f, 90f), null, Color.white);
        profileTitle.rectTransform.anchoredPosition = new Vector2(-276f, -82f);
        profileTitle.rectTransform.sizeDelta = new Vector2(222f, 56f);
        profileTitle.fontSize = 34f;
        profileTitle.fontStyle = FontStyles.Bold;
        profileTitle.color = new Color32(239, 239, 234, 255);
    }

    private static Image ProfileImage(string name, Transform parent, Vector2 position,
        Vector2 size, Sprite sprite, Color color)
    {
        Transform existing = parent.Find(name);
        var go = existing != null ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        if (existing == null) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        if (sprite != null && sprite.border != Vector4.zero)
            image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = sprite == null;
        return image;
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
        if (turnIntro != null) turnIntro.Hide();
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartMatch);
        if (backButton != null) backButton.onClick.RemoveListener(BackToHub);
        if (closeProfileButton != null) closeProfileButton.onClick.RemoveListener(CloseProfile);
        if (generatedProfiles != null)
            for (int i = 0; i < generatedProfiles.Length; i++)
                if (generatedProfiles[i] && turnProfiles[i] != null) Destroy(turnProfiles[i]);
    }
}
