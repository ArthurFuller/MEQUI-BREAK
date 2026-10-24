using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class RushBalanceController : MonoBehaviour
{

    public enum AppearanceMode { BorderedClothes, FullBody }

    [System.Serializable]
    public struct FullBodyOption
    {
        public Sprite Sprite;
        public Color SkinColor;
    }

    public readonly struct NpcAppearance
    {
        public readonly int HatIndex;
        public readonly int TorsoIndex;
        public readonly int ColorIndex;
        public readonly Sprite HatSprite;
        public readonly Sprite TorsoSprite;
        public readonly Sprite FullBodySprite;
        public readonly Color BodyColor;
        public readonly Vector2 HatOffset;
        public readonly float HatScale;
        public readonly float HatRotation;

        public NpcAppearance(
            int hatIndex,
            int torsoIndex,
            int colorIndex,
            Sprite hatSprite,
            Sprite torsoSprite,
            Sprite fullBodySprite,
            Color bodyColor,
            Vector2 hatOffset,
            float hatScale,
            float hatRotation)
        {
            HatIndex = hatIndex;
            TorsoIndex = torsoIndex;
            ColorIndex = colorIndex;
            HatSprite = hatSprite;
            TorsoSprite = torsoSprite;
            FullBodySprite = fullBodySprite;
            BodyColor = bodyColor;
            HatOffset = hatOffset;
            HatScale = hatScale;
            HatRotation = hatRotation;
        }
    }

    [SerializeField, HideInInspector] private int visualLayoutVersion;

    [Header("Sessão e pool da Hierarchy")]
    [SerializeField] private RushTimedSessionController session;
    [Tooltip("Cinco clientes: quatro visíveis e um reserva.")]
    [SerializeField] private RushCustomerView[] customers;
    [SerializeField] private RushOrderView[] orderViews;
    [SerializeField] private RectTransform[] queueAnchors;
    [SerializeField] private RushQueueSlot[] queueTargets;

    [Header("Fila visual dos clientes")]
    [SerializeField] private RectTransform[] customerAnchors;
    [SerializeField] private RectTransform customerEntrance;
    [SerializeField] private RectTransform customerExit;
    [SerializeField] private TMP_Text feedback;
    [SerializeField] private GameObject finalCycleBanner;
    [SerializeField] private RectTransform finalCyclePanel;
    [SerializeField] private CanvasGroup finalCycleGroup;
    [SerializeField] private TMP_Text finalCycleLabel;
    [Tooltip("Pausa após a saída completa, antes de a fila avançar.")]
    [SerializeField, Min(0f)] private float replacementPause = .18f;
    [Tooltip("Tempo reservado para a fila avançar e o novo cliente entrar.")]
    [SerializeField, Min(.01f)] private float replacementSettleSeconds = .34f;

    [Header("Aviso de ciclo final")]
    [SerializeField, Min(.01f)] private float finalNoticeEnterSeconds = .22f;
    [SerializeField, Min(0f)] private float finalNoticeHoldSeconds = 1.15f;
    [SerializeField, Min(.01f)] private float finalNoticeExitSeconds = .2f;
    [SerializeField, Min(0f)] private float finalNoticeOffset = 55f;

    [Header("Balanceamento inicial")]
    [Tooltip("Tempos para pedidos de um, dois e três itens.")]
    [SerializeField] private Vector3 preparationTimes = new Vector3(4f, 7f, 10f);
    [Tooltip("Paciência: paciente, normal e exigente.")]
    [SerializeField] private Vector3 patienceTimes = new Vector3(35f, 30f, 25f);
    [SerializeField, Range(0f, 1f)] private float secondarySpeed = .5f;

    [Header("Chegadas e sorteio justo")]
    [SerializeField] private bool staggerInitialCustomers;
    [Tooltip("Instantes das quatro chegadas, em segundos desde o inicio da partida.")]
    [SerializeField] private Vector4 initialArrivalSeconds = new Vector4(1f, 2f, 3.5f, 6f);
    [SerializeField] private bool fairRandomOrders;
    [Tooltip("Margem para selecionar o novo pedido, em segundos de simulacao.")]
    [SerializeField, Min(0f)] private float orderSafetySeconds = 3f;
    [Tooltip("Tempo estimado de reacao para cada entrega, em segundos de simulacao.")]
    [SerializeField, Min(0f)] private float deliveryReactionSeconds = .5f;
    [Header("Sons dos carimbos")]
    [SerializeField] private AudioClip deliveredStampSound;
    [SerializeField] private AudioClip cancelledStampSound;
    [SerializeField, Range(0f, 1f)] private float stampSoundVolume = .75f;
    private float initialArrivalClock;
    private int nextInitialCustomer;

    [Header("Aparência randômica dos NPCs")]
    [SerializeField] private AppearanceMode appearanceMode = AppearanceMode.BorderedClothes;
    [SerializeField] private FullBodyOption[] fullBodyOptions;
    [SerializeField] private AvatarCustomizationCatalog customizationCatalog;
    [Tooltip("Índice 0 deve representar a opção sem chapéu.")]
    [SerializeField] private Sprite[] npcHatOptions;
    [SerializeField, Tooltip("Chapéu excluído apenas do sorteio dos clientes; mantém o índice e a customização.")]
    private Sprite excludedNpcHat;
    [SerializeField] private Sprite[] npcTorsoOptions;
    [SerializeField] private Sprite[] orderFoodOptions;
    [SerializeField] private Color[] npcColorOptions;
    [SerializeField] private bool allowNoHat = true;
    [SerializeField, Range(0f, 1f)] private float noHatChance = .18f;
    [SerializeField] private bool avoidRepeatedHats = true;
    [SerializeField] private bool avoidRepeatedTorsos = true;
    [SerializeField] private bool avoidRepeatedColors = true;
    [SerializeField] private AvatarHatVisualAdjustment[] rushHatAdjustments;

    [Header("Prévia dos chapéus no Rush — Editor")]
    [SerializeField] private RushCustomerView hatPreviewCustomer;
    [SerializeField] private GameObject hatSafeAreaGuide;
    [SerializeField] private bool showHatSafeArea = true;
    [SerializeField] private bool previewHatInEditor = true;
    [SerializeField] private bool editHatDirectlyInScene;
    [SerializeField, Min(0)] private int hatPreviewIndex = 1;
    [SerializeField, Min(0)] private int colorPreviewIndex;

    [Header("Ritmo configurável")]
    [Tooltip("Multiplica preparo e perda de paciência. Não altera a duração total da partida.")]
    [SerializeField, Range(.25f, 3f)] private float simulationSpeed = 1f;
    [Tooltip("Multiplica a velocidade das animações do Rush. 0,5 = lento; 2 = rápido.")]
    [SerializeField, Range(.25f, 3f)] private float animationSpeed = 1f;

    [Header("Animação da fila de cards")]
    [Tooltip("Duração do movimento dos cards para a próxima posição.")]
    [SerializeField, Min(.01f)] private float queueCardMoveSeconds = .32f;
    [SerializeField] private Ease queueCardMoveEase = Ease.OutCubic;

    [Header("Selecao da fila - referencias opcionais da cena")]
    [SerializeField] private Button queueLeft;
    [SerializeField] private Button queueRight;
    [SerializeField] private CanvasGroup queueButtons;
    [SerializeField, Min(1f)] private float idleHintSeconds = 7f;
    private int selectedOrder = -1;
    private float lastQueueInteraction;
    private Tween arrowHint;
    private Vector3 arrowBaseScale;
    private PointerEventData queuePointer;
    private EventSystem queueEventSystem;
    private readonly List<RaycastResult> queueHits = new List<RaycastResult>(16);
    public bool QueueControlsEnabled => queueLeft != null && queueRight != null && queueButtons != null;

    private readonly List<int> customerLine = new List<int>(4);
    private readonly int[] pendingIds = new int[4];
    private readonly bool[] pendingDelivered = new bool[4];
    private readonly bool[] visualReserved = new bool[4];
    private readonly int[] telemetryOrderIds = new int[4];
    private int nextTelemetryOrderId;
    private RushRound round;
    private Sequence replacementSequence;
    private Sequence finalNoticeSequence;
    private Vector2 finalNoticeBasePosition;
    private int bufferCustomer = 4;
    private int pendingCount;
    private float feedbackClock;
    private bool processingResolution;
    private int visualResolutionsPending;
    public bool HoldQueuePositions => visualResolutionsPending > 0;
    public float QueueCardMoveSeconds => AnimationSeconds(queueCardMoveSeconds);
    public Ease QueueCardMoveEase => queueCardMoveEase;

    private bool finalCycle;
    private bool configured;
    private bool ending;
    private bool firstEntryHint;
    private int nextHintCustomer;
    private float nextHintTime;

    private void Awake()
    {
        configured = ValidateSetup();
        if (session != null) session.RegisterGameplay(configured);
        if (!configured)
        {
            enabled = false;
            return;
        }

        round = new RushRound(4);
        round.Resolved += Resolved;
        for (int i = 0; i < customers.Length; i++) customers[i].Initialize(this, i);
        for (int i = 0; i < orderViews.Length; i++) orderViews[i].Clear();
        for (int i = 0; i < queueTargets.Length; i++) queueTargets[i].Highlight(false);
        finalNoticeBasePosition = finalCyclePanel.anchoredPosition;
        finalCycleBanner.SetActive(false);
        if (hatSafeAreaGuide != null) hatSafeAreaGuide.SetActive(false);
        if (QueueControlsEnabled)
        {
            queueLeft.onClick.AddListener(MoveQueueLeft);
            queueRight.onClick.AddListener(MoveQueueRight);
            arrowBaseScale = queueButtons.transform.localScale;
            TouchQueueControls();
            RefreshQueueControls();
        }
    }

    private void OnEnable()
    {
        if (!configured) return;
        session.MatchStarted += BeginMatch;
        session.TimeExpired += BeginFinalCycle;
    }

    private void OnDisable()
    {
        firstEntryHint = false;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ApplyHatPreviewInEditor;
#endif
        SelectQueueCard(-1);
        arrowHint?.Kill();
        if (QueueControlsEnabled) queueButtons.transform.localScale = arrowBaseScale;
        replacementSequence?.Kill();
        finalNoticeSequence?.Kill();
        replacementSequence = null;
        finalNoticeSequence = null;
        if (session != null)
        {
            session.MatchStarted -= BeginMatch;
            session.TimeExpired -= BeginFinalCycle;
        }
        if (round != null) round.Resolved -= Resolved;
    }

    private void BeginMatch()
    {
        PlayerManager player = PlayerManager.Instance;
        firstEntryHint = player?.Profile != null && !player.Profile.RushBalanceHintShown;
        if (firstEntryHint)
        {
            player.Profile.RushBalanceHintShown = true;
            player.SaveProfile();
        }
        nextHintCustomer = 0;
        nextHintTime = Time.unscaledTime + .8f;
        replacementSequence?.Kill();
        replacementSequence = null;
        ending = false;
        finalCycle = false;
        processingResolution = false;
        visualResolutionsPending = 0;
        for (int i = 0; i < visualReserved.Length; i++) visualReserved[i] = false;
        pendingCount = 0;
        nextTelemetryOrderId = 0;
        if (feedback != null) feedback.text = string.Empty;
        finalNoticeSequence?.Kill();
        finalNoticeSequence = null;
        finalCyclePanel.anchoredPosition = finalNoticeBasePosition;
        finalCyclePanel.localScale = Vector3.one;
        finalCycleGroup.alpha = 0f;
        finalCycleBanner.SetActive(false);
        round.Reset();
        SelectQueueCard(-1);
        TouchQueueControls();
        customerLine.Clear();
        bufferCustomer = 4;
        initialArrivalClock = 0f;
        nextInitialCustomer = 0;

        for (int i = 0; i < customers.Length; i++) customers[i].Clear();
        for (int i = 0; i < orderViews.Length; i++) orderViews[i].Clear();

        for (int id = 0; !staggerInitialCustomers && id < 4; id++)
        {
            customerLine.Add(id);
            ActivateOrder(id, id, customerAnchors[id], true);
        }

        // A própria fila e suas setas indicam que os pedidos podem ser movidos.
    }

    private void Update()
    {
        if (QueueControlsEnabled) UpdateQueueInput();
        if (!configured || !session.IsPlaying || ending) return;
        round.Tick(Time.deltaTime * simulationSpeed, secondarySpeed);
        if (staggerInitialCustomers && !finalCycle)
        {
            initialArrivalClock += Time.deltaTime;
            while (nextInitialCustomer < 4
                && initialArrivalClock >= initialArrivalSeconds[nextInitialCustomer])
            {
                int id = nextInitialCustomer++;
                customerLine.Insert(0, id);
                ActivateOrder(id, id, CustomerLineAnchor(0), false);
                for (int i = 1; i < customerLine.Count; i++)
                    customers[customerLine[i]].MoveTo(CustomerLineAnchor(i));
            }
        }
        Refresh();
        UpdateFirstEntryHint();

        if (feedback != null && feedbackClock > 0f && (feedbackClock -= Time.deltaTime) <= 0f)
            feedback.text = string.Empty;
    }

    private void UpdateFirstEntryHint()
    {
        if (!firstEntryHint || Time.unscaledTime < nextHintTime) return;
        for (int offset = 0; offset < customers.Length; offset++)
        {
            int index = (nextHintCustomer + offset) % customers.Length;
            RushCustomerView customer = customers[index];
            int id = customer.OrderId;
            if (id < 0 || round.Orders[id].State != RushRound.Status.Waiting || !customer.PulseStartHint()) continue;
            nextHintCustomer = (index + 1) % customers.Length;
            nextHintTime = Time.unscaledTime + .9f;
            break;
        }
    }

    private void ActivateOrder(int id, int customerIndex, RectTransform target, bool instant)
    {
        int items = Random.Range(1, 4);
        RushCustomerView.PatienceProfile profile =
            (RushCustomerView.PatienceProfile)Random.Range(0, 3);
        if (fairRandomOrders) PickFairOrder(out items, out profile);
        float patience = patienceTimes[(int)profile];
        float duration = preparationTimes[items - 1];

        bool arriving = staggerInitialCustomers && !instant;
        if (!round.Activate(id, customerIndex, items, patience, duration, arriving)) return;
        telemetryOrderIds[id] = ++nextTelemetryOrderId;
        EventLogger.Instance?.RecordActivityEvent("rush_order_created:" + telemetryOrderIds[id] + ":" + items + ":" + profile.ToString().ToLowerInvariant());
        RushRound.Order order = round.Orders[id];
        customers[customerIndex].Enter(id, order, profile, customerEntrance, target, instant,
            arriving ? (System.Action)(() => round.FinishArrival(id)) : null);
        NpcAppearance appearance = CreateAppearance(customerIndex);
        customers[customerIndex].ApplyAppearance(appearance);
        Sprite[] foodSprites = CreateFoodSprites(items);
        customers[customerIndex].SetFoodSprites(foodSprites);
        orderViews[id].Bind(
            this,
            id,
            items,
            appearance,
            customers[customerIndex].CurrentFaceSprite);
        orderViews[id].SetFoodSprites(foodSprites);
        EventLogger.Instance?.RecordActivityEvent(
            $"rush_request_{items}_items_{profile.ToString().ToLowerInvariant()}");
    }

    private RectTransform CustomerLineAnchor(int position)
    {
        // A partial line fills from the right: existing customers keep their
        // places while each new arrival occupies the next space to the left.
        return customerAnchors[customerAnchors.Length - customerLine.Count + position];
    }

    private void PickFairOrder(out int items, out RushCustomerView.PatienceProfile profile)
    {
        // Reservoir sampling keeps all eligible combinations equally likely,
        // without unbounded retries or allocations. Existing delays caused by
        // player choices are not undone and existing patience is never extended.
        int eligible = 0;
        int selectedSize = 0;
        int selectedProfile = 0;
        for (int i = 1; i < 3; i++)
        {
            if (preparationTimes[i] < preparationTimes[selectedSize]) selectedSize = i;
            if (patienceTimes[i] > patienceTimes[selectedProfile]) selectedProfile = i;
        }
        for (int size = 0; size < 3; size++)
        {
            float estimate = round.EstimateNewOrderSeconds(
                preparationTimes[size], secondarySpeed, deliveryReactionSeconds) + orderSafetySeconds;
            for (int patience = 0; patience < 3; patience++)
            {
                if (patienceTimes[patience] < estimate) continue;
                if (Random.Range(0, ++eligible) != 0) continue;
                selectedSize = size;
                selectedProfile = patience;
            }
        }
        items = selectedSize + 1;
        profile = (RushCustomerView.PatienceProfile)selectedProfile;
    }

    private Sprite[] CreateFoodSprites(int count)
    {
        Sprite[] result = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            int index;
            bool repeated;
            do
            {
                index = Random.Range(0, orderFoodOptions.Length);
                repeated = false;
                for (int j = 0; j < i; j++)
                    if (result[j] == orderFoodOptions[index]) repeated = true;
            } while (repeated);
            result[i] = orderFoodOptions[index];
        }
        return result;
    }

    private NpcAppearance CreateAppearance(int customerIndex)
    {
        int hatIndex = 0;
        int colorIndex = 0;
        int torsoIndex = 0;

        // Apenas quatro clientes ficam visíveis. Uma busca curta e limitada
        // evita repetições sem alocar listas ou executar lógica a cada frame.
        for (int attempt = 0; attempt < 48; attempt++)
        {
            hatIndex = PickHatIndex();
            bool fullBody = appearanceMode == AppearanceMode.FullBody;
            torsoIndex = Random.Range(0, fullBody ? fullBodyOptions.Length : npcTorsoOptions.Length);
            colorIndex = fullBody ? torsoIndex : Random.Range(0, npcColorOptions.Length);
            if (AppearanceIsAvailable(customerIndex, hatIndex, torsoIndex, colorIndex)) break;
        }

        return BuildAppearance(hatIndex, colorIndex, torsoIndex);
    }

    private NpcAppearance BuildAppearance(int hatIndex, int colorIndex, int torsoIndex)
    {
        hatIndex = Mathf.Clamp(hatIndex, 0, npcHatOptions.Length - 1);
        bool fullBody = appearanceMode == AppearanceMode.FullBody;
        torsoIndex = Mathf.Clamp(torsoIndex, 0,
            (fullBody ? fullBodyOptions.Length : npcTorsoOptions.Length) - 1);
        colorIndex = fullBody ? torsoIndex : Mathf.Clamp(colorIndex, 0, npcColorOptions.Length - 1);
        Sprite hat = npcHatOptions[hatIndex];
        AvatarCustomizationItem catalogAdjustment = customizationCatalog.GetItem(
            AvatarCustomizationCategory.Hat, hatIndex);
        AvatarHatVisualAdjustment rushAdjustment = rushHatAdjustments != null
            && hatIndex < rushHatAdjustments.Length ? rushHatAdjustments[hatIndex] : null;
        Vector2 baseOffset = catalogAdjustment != null
            ? catalogAdjustment.DeslocamentoNoAvatar : Vector2.zero;
        float baseScale = catalogAdjustment != null && catalogAdjustment.EscalaNoAvatar > 0f
            ? catalogAdjustment.EscalaNoAvatar : 1f;
        float baseRotation = catalogAdjustment != null ? catalogAdjustment.RotacaoNoAvatar : 0f;
        Vector2 rushOffset = rushAdjustment != null ? rushAdjustment.Offset : Vector2.zero;
        float rushScale = rushAdjustment != null && rushAdjustment.ScaleMultiplier > 0f
            ? rushAdjustment.ScaleMultiplier : 1f;
        float rushRotation = rushAdjustment != null ? rushAdjustment.Rotation : 0f;
        return new NpcAppearance(
            hatIndex,
            torsoIndex,
            colorIndex,
            hat,
            fullBody ? null : npcTorsoOptions[torsoIndex],
            fullBody ? fullBodyOptions[torsoIndex].Sprite : null,
            fullBody ? fullBodyOptions[torsoIndex].SkinColor : npcColorOptions[colorIndex],
            baseOffset + rushOffset,
            baseScale * rushScale,
            baseRotation + rushRotation);
    }

    private int PickHatIndex()
    {
        if (allowNoHat && Random.value < noHatChance) return 0;
        int excluded = System.Array.IndexOf(npcHatOptions, excludedNpcHat);
        int count = npcHatOptions.Length - 1 - (excluded > 0 ? 1 : 0);
        if (count <= 0) return 0;
        int selected = Random.Range(1, count + 1);
        return excluded > 0 && selected >= excluded ? selected + 1 : selected;
    }

    private bool AppearanceIsAvailable(int customerIndex, int hatIndex, int torsoIndex, int colorIndex)
    {
        for (int i = 0; i < customers.Length; i++)
        {
            RushCustomerView customer = customers[i];
            if (i == customerIndex || customer == null || !customer.HasAppearance
                || !customer.gameObject.activeSelf) continue;
            if (avoidRepeatedHats && customer.HatIndex == hatIndex) return false;
            if (avoidRepeatedTorsos && customer.TorsoIndex == torsoIndex) return false;
            if (avoidRepeatedColors && customer.ColorIndex == colorIndex) return false;
        }
        return true;
    }

    public void Queue(int id)
    {
        if (!session.IsPlaying || ending || !round.Enqueue(id)) return;
        firstEntryHint = false;
        TouchQueueControls();
        EventLogger.Instance?.RecordUserAction("rush_select_request");
        AudioManager.Instance?.PlayQueueAccepted();
        MequiHaptics.Selection();
        Refresh();
    }

    public void Reorder(int id, int rank)
    {
        if (!session.IsPlaying || ending || !round.Move(id, rank)) return;
        TouchQueueControls();
        session.RecordReorder();
        EventLogger.Instance?.RecordActivityEvent("rush_order_reordered:" + telemetryOrderIds[id] + ":" + rank);
        AudioManager.Instance?.PlayQueueReordered();
        MequiHaptics.Selection();
        Refresh();
    }

    public void ReorderOnto(int id, int targetId)
    {
        if (!session.IsPlaying || round == null) return;
        Reorder(id, round.Rank(targetId));
    }

    public void Deliver(int id)
    {
        if (!session.IsPlaying || ending) return;
        if (round.Deliver(id))
        {
            EventLogger.Instance?.RecordUserAction("rush_deliver_ready_order");
            return;
        }

        Message("Este pedido ainda não está pronto.");
        AudioManager.Instance?.PlayError();
        MequiHaptics.Reject();
    }

    public bool CanDrag(int id)
    {
        if (!session.IsPlaying || ending || round == null || id < 0 || id >= round.Orders.Length)
            return false;
        foreach (RushOrderView view in orderViews)
            if (view.IsDragging && view.OrderId != id) return false;
        return round.Orders[id].State == RushRound.Status.Queued;
    }

    public int Rank(int id) => round != null ? round.Rank(id) : -1;

    public void RestoreQueueSiblingOrder(int preferredTopOrderId)
    {
        if (round == null || orderViews == null || orderViews.Length == 0) return;
        Transform parent = null;
        int firstSibling = int.MaxValue;
        foreach (RushOrderView view in orderViews)
        {
            if (view == null) continue;
            if (parent == null) parent = view.transform.parent;
            if (view.transform.parent == parent)
                firstSibling = Mathf.Min(firstSibling, view.transform.GetSiblingIndex());
        }
        if (parent == null || firstSibling == int.MaxValue) return;

        bool keepReadyCardOnTop = IsReady(preferredTopOrderId);
        int nextSibling = firstSibling;
        for (int readyPass = 0; readyPass < 2; readyPass++)
        {
            for (int rank = 0; rank < round.QueueCount; rank++)
            {
                foreach (RushOrderView view in orderViews)
                {
                    if (view == null || view.transform.parent != parent || Rank(view.OrderId) != rank)
                        continue;
                    if (keepReadyCardOnTop && view.OrderId == preferredTopOrderId)
                        continue;
                    if (IsReady(view.OrderId) != (readyPass == 1))
                        continue;

                    view.transform.SetSiblingIndex(nextSibling++);
                    break;
                }
            }
        }

        if (keepReadyCardOnTop)
        {
            foreach (RushOrderView view in orderViews)
            {
                if (view != null && view.OrderId == preferredTopOrderId && view.transform.parent == parent)
                {
                    view.transform.SetSiblingIndex(nextSibling);
                    break;
                }
            }
        }
    }

    public bool IsReady(int id)
    {
        return round != null && id >= 0 && id < round.Orders.Length
            && round.Orders[id].State == RushRound.Status.Ready;
    }

    public float AnimationSeconds(float baseSeconds)
    {
        return Mathf.Max(.001f, baseSeconds / Mathf.Max(.25f, animationSpeed));
    }

    public void PlayStampSound(bool delivered)
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;
        AudioClip clip = delivered ? deliveredStampSound : cancelledStampSound;
        if (clip != null) audio.PlayEffect(clip, stampSoundVolume);
        else if (delivered) audio.PlayConfirm();
        else audio.PlayError();
    }

    public RectTransform Anchor(int id)
    {
        int rank = Rank(id);
        return rank >= 0 && rank < queueAnchors.Length ? queueAnchors[rank] : null;
    }

    public void Highlight(int id, bool value)
    {
        if (!configured || round == null) return;
        bool queued = id >= 0 && id < round.Orders.Length
            && round.Orders[id].State == RushRound.Status.Queued;
        for (int i = 0; i < queueTargets.Length; i++)
            queueTargets[i].Highlight(value && queued && i < round.QueueCount);
    }

    private void Refresh()
    {
        for (int id = 0; id < round.Orders.Length; id++)
        {
            RushRound.Order order = round.Orders[id];
            if (order.State == RushRound.Status.Hidden
                || order.Customer < 0 || order.Customer >= customers.Length) continue;
            RushCustomerView customer = customers[order.Customer];
            if (customer.OrderId != id) continue;
            customer.Refresh(order);
            orderViews[id].Refresh(order, customer.BodyColor, customer.CurrentFaceSprite);
        }

        foreach (RushOrderView view in orderViews)
            if (view.IsDragging) Highlight(view.OrderId, true);
        RefreshQueueControls();
    }

    public void SelectQueueCard(int id)
    {
        if (!QueueControlsEnabled) return;
        if (id >= 0 && !CanDrag(id)) return;
        TouchQueueControls();
        selectedOrder = id;
        foreach (RushOrderView view in orderViews)
            if (view != null) view.SetSelected(view.OrderId == selectedOrder && selectedOrder >= 0);
        RefreshQueueControls();
    }

    private void MoveQueueLeft() => MoveSelected(-1);
    private void MoveQueueRight() => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        if (!QueueControlsEnabled || selectedOrder < 0 || HoldQueuePositions || !CanDrag(selectedOrder)) return;
        foreach (RushOrderView view in orderViews)
            if (view.IsDragging) return;
        Reorder(selectedOrder, Rank(selectedOrder) + offset);
    }

    private void TouchQueueControls()
    {
        lastQueueInteraction = Time.unscaledTime;
        arrowHint?.Kill();
        if (QueueControlsEnabled) queueButtons.transform.localScale = arrowBaseScale;
    }

    private void RefreshQueueControls()
    {
        if (!QueueControlsEnabled) return;
        if (selectedOrder >= 0 && !CanDrag(selectedOrder))
        {
            selectedOrder = -1;
            foreach (RushOrderView view in orderViews) view.SetSelected(false);
        }
        bool selected = selectedOrder >= 0;
        bool canMove = selected && !HoldQueuePositions;
        foreach (RushOrderView view in orderViews)
            if (view.IsDragging) canMove = false;
        queueButtons.alpha = selected ? 1f : .3f;
        queueLeft.interactable = canMove && Rank(selectedOrder) > 0;
        queueRight.interactable = canMove && Rank(selectedOrder) < round.QueueCount - 1;
    }

    private void UpdateQueueInput()
    {
        if (!configured || !session.IsPlaying || ending)
        {
            if (selectedOrder >= 0) SelectQueueCard(-1);
            TouchQueueControls();
            return;
        }
        bool pressed = false;
        Vector2 position = default;
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pressed = true;
            position = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressed = true;
            position = Mouse.current.position.ReadValue();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            pressed = true;
            position = Input.GetTouch(0).position;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            pressed = true;
            position = Input.mousePosition;
        }
#endif
        if (pressed)
        {
            TouchQueueControls();
            if (selectedOrder >= 0 && EventSystem.current != null)
            {
                if (queuePointer == null || queueEventSystem != EventSystem.current)
                {
                    queueEventSystem = EventSystem.current;
                    queuePointer = new PointerEventData(EventSystem.current);
                }
                queuePointer.position = position;
                queueHits.Clear();
                EventSystem.current.RaycastAll(queuePointer, queueHits);
                Transform hit = queueHits.Count > 0 ? queueHits[0].gameObject.transform : null;
                bool keep = hit != null && (hit.IsChildOf(queueLeft.transform) || hit.IsChildOf(queueRight.transform));
                foreach (RushOrderView view in orderViews)
                    if (hit != null && view.OrderId == selectedOrder && hit.IsChildOf(view.transform)) keep = true;
                if (!keep) SelectQueueCard(-1);
            }
        }
        foreach (RushOrderView view in orderViews)
            if (view.IsDragging) { TouchQueueControls(); return; }
        if (selectedOrder < 0 && round.QueueCount > 0 && !HoldQueuePositions
            && Time.unscaledTime - lastQueueInteraction >= idleHintSeconds)
        {
            TouchQueueControls();
            arrowHint = queueButtons.transform.DOPunchScale(Vector3.one * .045f, .5f, 2, .25f).SetTarget(this);
        }
    }

    private void Resolved(int id, bool delivered)
    {
        session.RecordOrder(delivered);
        EventLogger.Instance?.RecordActivityEvent("rush_order_resolved:" + telemetryOrderIds[id] + ":" + (delivered ? "delivered" : "missed"));
        if (ending)
        {
            orderViews[id].Clear();
            return;
        }

        // Cada card responde imediatamente; apenas a reposicao de clientes entra na fila.
        orderViews[id].HoldForResolution();
        if (orderViews[id].gameObject.activeSelf && !visualReserved[id])
        {
            visualReserved[id] = true;
            visualResolutionsPending++;
        }

        int customerIndex = round.Orders[id].Customer;
        if (delivered)
        {
            Message("Pedido entregue!");
            MequiHaptics.Confirm();
            orderViews[id].PlayDelivery(
                customers[customerIndex].DeliveryTarget,
                () =>
                {
                    QueueCustomerReplacement(id, true);
                });
        }
        else
        {
            Message("A paciência acabou.");
            MequiHaptics.Reject();
            orderViews[id].PlayFailure(() =>
            {
                QueueCustomerReplacement(id, false);
            });
        }
        Refresh();
    }

    private void QueueCustomerReplacement(int id, bool delivered)
    {
        if (ending) return;
        ReleaseVisualReservation(id);
        pendingIds[pendingCount] = id;
        pendingDelivered[pendingCount++] = delivered;
        if (!processingResolution) ProcessNextResolution();
    }

    private void ProcessNextResolution()
    {
        if (ending || pendingCount == 0)
        {
            processingResolution = false;
            return;
        }

        processingResolution = true;
        int id = pendingIds[0];
        bool delivered = pendingDelivered[0];
        for (int i = 1; i < pendingCount; i++)
        {
            pendingIds[i - 1] = pendingIds[i];
            pendingDelivered[i - 1] = pendingDelivered[i];
        }
        pendingCount--;
        BeginCustomerReplacement(id, round.Orders[id].Customer, delivered);
    }

    private void ReleaseVisualReservation(int id)
    {
        if (!visualReserved[id]) return;
        visualReserved[id] = false;
        visualResolutionsPending--;
        Refresh();
    }

    private void BeginCustomerReplacement(int id, int departingCustomer, bool delivered)
    {
        if (ending) return;
        int departedPosition = customerLine.IndexOf(departingCustomer);
        if (departedPosition < 0)
        {
            ProcessNextResolution();
            return;
        }

        customers[departingCustomer].Depart(customerExit, delivered, () =>
        {
            if (ending) return;

            if (finalCycle)
            {
                customerLine.Remove(departingCustomer);
                orderViews[id].Clear();
                ProcessNextResolution();
                TryCompleteFinalCycle();
                return;
            }

            // O lugar permanece vazio por um instante. Somente depois a fila avança,
            // evitando que o cliente novo atravesse o personagem que ainda sai.
            replacementSequence?.Kill();
            replacementSequence = DOTween.Sequence()
                .SetTarget(this)
                .AppendInterval(AnimationSeconds(replacementPause))
                .AppendCallback(() =>
                {
                    if (ending) return;

                    if (finalCycle)
                    {
                        customerLine.Remove(departingCustomer);
                        orderViews[id].Clear();
                        return;
                    }

                    int currentPosition = customerLine.IndexOf(departingCustomer);
                    if (currentPosition < 0) return;

                    int incomingCustomer = bufferCustomer;
                    bufferCustomer = departingCustomer;
                    customerLine.RemoveAt(currentPosition);
                    customerLine.Insert(0, incomingCustomer);

                    ActivateOrder(id, incomingCustomer, CustomerLineAnchor(0), false);
                    for (int i = 1; i < customerLine.Count; i++)
                        customers[customerLine[i]].MoveTo(CustomerLineAnchor(i));
                })
                .AppendInterval(AnimationSeconds(replacementSettleSeconds))
                .OnComplete(() =>
                {
                    replacementSequence = null;
                    if (!ending)
                    {
                        ProcessNextResolution();
                        TryCompleteFinalCycle();
                    }
                });
        });
    }

    private void BeginFinalCycle()
    {
        if (ending || finalCycle) return;

        finalCycle = true;
        ShowFinalCycleNotice();
        TryCompleteFinalCycle();
    }

    private void ShowFinalCycleNotice()
    {
        finalNoticeSequence?.Kill();
        finalCycleLabel.text = "TEMPO ENCERRADO\nFinalize os clientes atuais";
        finalCycleBanner.SetActive(true);
        finalCycleGroup.alpha = 0f;
        finalCyclePanel.anchoredPosition = finalNoticeBasePosition + Vector2.up * finalNoticeOffset;
        finalCyclePanel.localScale = Vector3.one * .94f;

        finalNoticeSequence = DOTween.Sequence().SetTarget(this)
            .Join(finalCycleGroup.DOFade(1f, AnimationSeconds(finalNoticeEnterSeconds)).SetEase(Ease.OutQuad))
            .Join(finalCyclePanel.DOAnchorPos(finalNoticeBasePosition, AnimationSeconds(finalNoticeEnterSeconds)).SetEase(Ease.OutCubic))
            .Join(finalCyclePanel.DOScale(1f, AnimationSeconds(finalNoticeEnterSeconds)).SetEase(Ease.OutBack))
            .AppendInterval(AnimationSeconds(finalNoticeHoldSeconds))
            .Append(finalCycleGroup.DOFade(0f, AnimationSeconds(finalNoticeExitSeconds)).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                finalNoticeSequence = null;
                finalCycleBanner.SetActive(false);
            });
    }

    private void TryCompleteFinalCycle()
    {
        if (!finalCycle || ending || processingResolution || pendingCount > 0 || visualResolutionsPending > 0
            || (replacementSequence != null && replacementSequence.IsActive())) return;

        for (int i = 0; i < round.Orders.Length; i++)
        {
            RushRound.Status state = round.Orders[i].State;
            if (state == RushRound.Status.Waiting || state == RushRound.Status.Queued
                || state == RushRound.Status.Ready) return;
        }

        ending = true;
        session.CompleteFinalCycle();
    }

    private void Message(string value)
    {
        if (feedback == null) return;
        feedback.text = value;
        feedbackClock = 1.7f;
    }

    private bool ValidateSetup()
    {
        if (session == null) return false;
        if (finalCycleBanner == null || finalCyclePanel == null || finalCycleGroup == null || finalCycleLabel == null)
            return false;
        if (customers == null || customers.Length != 5) return false;
        if (orderViews == null || orderViews.Length != 4) return false;
        if (queueAnchors == null || queueAnchors.Length != 4) return false;
        if (queueTargets == null || queueTargets.Length != 4) return false;
        if (customerAnchors == null || customerAnchors.Length != 4)
            return false;
        if (customerEntrance == null || customerExit == null) return false;
        if (replacementPause < 0f || replacementSettleSeconds <= 0f)
            return false;
        if (orderSafetySeconds < 0f || deliveryReactionSeconds < 0f
            || (staggerInitialCustomers && (initialArrivalSeconds.x < 0f
                || initialArrivalSeconds.y < initialArrivalSeconds.x
                || initialArrivalSeconds.z < initialArrivalSeconds.y
                || initialArrivalSeconds.w < initialArrivalSeconds.z)))
            return false;
        if (finalNoticeEnterSeconds <= 0f || finalNoticeHoldSeconds < 0f || finalNoticeExitSeconds <= 0f)
            return false;
        if (preparationTimes.x <= 0f || preparationTimes.y <= 0f || preparationTimes.z <= 0f
            || patienceTimes.x <= 0f || patienceTimes.y <= 0f || patienceTimes.z <= 0f
            || secondarySpeed < 0f || secondarySpeed > 1f
            || simulationSpeed <= 0f || animationSpeed <= 0f)
            return false;
        if (customizationCatalog == null) return false;
        if (npcHatOptions == null || npcHatOptions.Length < 2)
            return false;
        if (npcTorsoOptions == null || npcTorsoOptions.Length == 0)
            return false;
        for (int i = 0; i < npcTorsoOptions.Length; i++)
            if (npcTorsoOptions[i] == null) return false;
        if (appearanceMode == AppearanceMode.FullBody)
        {
            if (fullBodyOptions == null || fullBodyOptions.Length == 0)
                return false;
            for (int i = 0; i < fullBodyOptions.Length; i++)
                if (fullBodyOptions[i].Sprite == null)
                    return false;
        }
        if (orderFoodOptions == null || orderFoodOptions.Length < 3)
            return false;
        for (int i = 0; i < orderFoodOptions.Length; i++)
        {
            if (orderFoodOptions[i] == null) return false;
            for (int j = 0; j < i; j++)
                if (orderFoodOptions[i] == orderFoodOptions[j])
                    return false;
        }
        if (npcColorOptions == null || npcColorOptions.Length < 4)
            return false;
        if (rushHatAdjustments == null || rushHatAdjustments.Length != npcHatOptions.Length)
            return false;

        for (int i = 0; i < customers.Length; i++)
            if (customers[i] == null || !customers[i].Configured)
                return false;
        for (int i = 0; i < orderViews.Length; i++)
        {
            if (orderViews[i] == null || !orderViews[i].Configured)
                return false;
            if (queueAnchors[i] == null || queueTargets[i] == null || customerAnchors[i] == null)
                return false;
            if (!queueTargets[i].ConfiguredFor(this, i))
                return false;
        }
        return true;
    }

#if UNITY_EDITOR
    private int appliedHatPreviewIndex = -1;
    public RectTransform HatPreviewTransform => hatPreviewCustomer != null && hatPreviewCustomer.HatImage != null
        ? hatPreviewCustomer.HatImage.rectTransform : null;

    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyHatPreviewInEditor;
        UnityEditor.EditorApplication.delayCall += ApplyHatPreviewInEditor;
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        if (!previewHatInEditor || !editHatDirectlyInScene || hatPreviewCustomer == null
            || hatPreviewCustomer.HatImage == null
            || UnityEditor.Selection.activeGameObject != hatPreviewCustomer.HatImage.gameObject)
            return;

        SaveRushHatAdjustment();
    }

    public void ApplyHatPreviewInEditor()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyHatPreviewInEditor;
        if (this == null || Application.isPlaying) return;
        if (hatSafeAreaGuide != null) hatSafeAreaGuide.SetActive(showHatSafeArea);
        ApplyHatPreviewVisibilityInEditor();
        if (!previewHatInEditor || hatPreviewCustomer == null || customizationCatalog == null
            || npcHatOptions == null || npcHatOptions.Length == 0
            || npcColorOptions == null || npcColorOptions.Length == 0) return;

        hatPreviewIndex = Mathf.Clamp(hatPreviewIndex, 0, npcHatOptions.Length - 1);
        colorPreviewIndex = Mathf.Clamp(colorPreviewIndex, 0, npcColorOptions.Length - 1);
        hatPreviewCustomer.gameObject.SetActive(true);
        hatPreviewCustomer.ApplyAppearance(BuildAppearance(hatPreviewIndex, colorPreviewIndex, 0));
        appliedHatPreviewIndex = hatPreviewIndex;
        UnityEditor.SceneView.RepaintAll();
    }

    private void ApplyHatPreviewVisibilityInEditor()
    {
        if (customers == null) return;
        for (int i = 0; i < customers.Length; i++)
        {
            RushCustomerView customer = customers[i];
            if (customer != null)
                customer.SetHatEditorPreview(previewHatInEditor && customer == hatPreviewCustomer);
        }
    }

    public void SaveRushHatAdjustment()
    {
        if (Application.isPlaying || !previewHatInEditor || appliedHatPreviewIndex != hatPreviewIndex
            || npcHatOptions == null || npcHatOptions.Length == 0
            || customizationCatalog == null || hatPreviewCustomer == null
            || hatPreviewCustomer.HatImage == null) return;
        int index = Mathf.Clamp(hatPreviewIndex, 0, npcHatOptions.Length - 1);
        if (rushHatAdjustments == null || index >= rushHatAdjustments.Length) return;
        AvatarHatVisualAdjustment adjustment = rushHatAdjustments[index];
        if (adjustment == null) return;

        RectTransform target = hatPreviewCustomer.HatImage.rectTransform;
        Rect rect = target.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        AvatarCustomizationItem catalogAdjustment = customizationCatalog.GetItem(
            AvatarCustomizationCategory.Hat, index);
        Vector2 baseOffset = catalogAdjustment != null
            ? catalogAdjustment.DeslocamentoNoAvatar : Vector2.zero;
        float baseScale = catalogAdjustment != null && catalogAdjustment.EscalaNoAvatar > 0f
            ? catalogAdjustment.EscalaNoAvatar : 1f;
        float baseRotation = catalogAdjustment != null ? catalogAdjustment.RotacaoNoAvatar : 0f;
        Vector2 finalOffset = new Vector2(
            target.anchoredPosition.x / rect.width,
            target.anchoredPosition.y / rect.height);
        float finalScale = (Mathf.Abs(target.localScale.x) + Mathf.Abs(target.localScale.y))
            * .5f / hatPreviewCustomer.HatScaleMultiplier;
        float finalRotation = target.localEulerAngles.z;
        if (finalRotation > 180f) finalRotation -= 360f;

        Vector2 nextOffset = finalOffset - baseOffset - hatPreviewCustomer.HatModeOffset;
        float nextScale = Mathf.Max(.1f, finalScale / Mathf.Max(.01f, baseScale));
        float nextRotation = finalRotation - baseRotation;
        if (Vector2.SqrMagnitude(adjustment.Offset - nextOffset) < .0000001f
            && Mathf.Abs(adjustment.ScaleMultiplier - nextScale) < .0001f
            && Mathf.Abs(adjustment.Rotation - nextRotation) < .001f) return;

        UnityEditor.Undo.RecordObject(this, "Posicionar chapéu no Rush Balance");
        adjustment.Offset = nextOffset;
        adjustment.ScaleMultiplier = nextScale;
        adjustment.Rotation = nextRotation;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
