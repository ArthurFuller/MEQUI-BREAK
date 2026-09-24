using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RushBalanceController : MonoBehaviour
{
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

    [Header("Ritmo configurável")]
    [Tooltip("Multiplica preparo e perda de paciência. Não altera a duração total da partida.")]
    [SerializeField, Range(.25f, 3f)] private float simulationSpeed = 1f;
    [Tooltip("Multiplica a velocidade das animações do Rush. 0,5 = lento; 2 = rápido.")]
    [SerializeField, Range(.25f, 3f)] private float animationSpeed = 1f;

    private readonly List<int> customerLine = new List<int>(4);
    private readonly int[] pendingIds = new int[4];
    private readonly bool[] pendingDelivered = new bool[4];
    private RushRound round;
    private Sequence replacementSequence;
    private Sequence finalNoticeSequence;
    private Vector2 finalNoticeBasePosition;
    private int bufferCustomer = 4;
    private int pendingCount;
    private float feedbackClock;
    private bool processingResolution;
    private bool finalCycle;
    private bool configured;
    private bool ending;

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
    }

    private void OnEnable()
    {
        if (!configured) return;
        session.MatchStarted += BeginMatch;
        session.TimeExpired += BeginFinalCycle;
    }

    private void OnDisable()
    {
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
        replacementSequence?.Kill();
        replacementSequence = null;
        ending = false;
        finalCycle = false;
        processingResolution = false;
        pendingCount = 0;
        feedback.text = string.Empty;
        finalNoticeSequence?.Kill();
        finalNoticeSequence = null;
        finalCyclePanel.anchoredPosition = finalNoticeBasePosition;
        finalCyclePanel.localScale = Vector3.one;
        finalCycleGroup.alpha = 0f;
        finalCycleBanner.SetActive(false);
        round.Reset();
        customerLine.Clear();
        bufferCustomer = 4;

        for (int i = 0; i < customers.Length; i++) customers[i].Clear();
        for (int i = 0; i < orderViews.Length; i++) orderViews[i].Clear();

        for (int id = 0; id < 4; id++)
        {
            customerLine.Add(id);
            ActivateOrder(id, id, customerAnchors[id], true);
        }

        Message("Toque nos balões para organizar os pedidos.");
    }

    private void Update()
    {
        if (!configured || !session.IsPlaying || ending) return;
        round.Tick(Time.deltaTime * simulationSpeed, secondarySpeed);
        Refresh();

        if (feedbackClock > 0f && (feedbackClock -= Time.deltaTime) <= 0f)
            feedback.text = string.Empty;
    }

    private void ActivateOrder(int id, int customerIndex, RectTransform target, bool instant)
    {
        int items = Random.Range(1, 4);
        RushCustomerView.PatienceProfile profile =
            (RushCustomerView.PatienceProfile)Random.Range(0, 3);
        float patience = profile == RushCustomerView.PatienceProfile.Patient
            ? patienceTimes.x
            : profile == RushCustomerView.PatienceProfile.Normal ? patienceTimes.y : patienceTimes.z;
        float duration = items == 1
            ? preparationTimes.x
            : items == 2 ? preparationTimes.y : preparationTimes.z;

        if (!round.Activate(id, customerIndex, items, patience, duration)) return;
        RushRound.Order order = round.Orders[id];
        customers[customerIndex].Enter(id, order, profile, customerEntrance, target, instant);
        orderViews[id].Bind(
            this,
            id,
            items,
            customers[customerIndex].BodyColor,
            customers[customerIndex].CurrentFaceSprite);
        EventLogger.Instance?.RecordActivityEvent(
            $"rush_request_{items}_items_{profile.ToString().ToLowerInvariant()}");
    }

    public void Queue(int id)
    {
        if (!session.IsPlaying || ending || !round.Enqueue(id)) return;
        EventLogger.Instance?.RecordUserAction("rush_select_request");
        AudioManager.Instance?.PlayConfirm();
        MequiHaptics.Selection();
        Refresh();
    }

    public void Reorder(int id, int rank)
    {
        if (!session.IsPlaying || ending || !round.Move(id, rank)) return;
        session.RecordReorder();
        AudioManager.Instance?.PlayConfirm();
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

    public bool IsReady(int id)
    {
        return round != null && id >= 0 && id < round.Orders.Length
            && round.Orders[id].State == RushRound.Status.Ready;
    }

    public float AnimationSeconds(float baseSeconds)
    {
        return Mathf.Max(.001f, baseSeconds / Mathf.Max(.25f, animationSpeed));
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
            if (order.Customer < 0 || order.Customer >= customers.Length) continue;
            RushCustomerView customer = customers[order.Customer];
            customer.Refresh(order);
            orderViews[id].Refresh(order, customer.BodyColor, customer.CurrentFaceSprite);
        }

        foreach (RushOrderView view in orderViews)
            if (view.IsDragging) Highlight(view.OrderId, true);
    }

    private void Resolved(int id, bool delivered)
    {
        session.RecordOrder(delivered);
        if (ending)
        {
            orderViews[id].Clear();
            return;
        }

        // Mantém o card pronto visível enquanto entregas anteriores terminam.
        // Sem esta reserva visual, o Refresh ocultava o segundo card porque seu
        // estado lógico já havia mudado para Delivered antes da animação começar.
        if (delivered) orderViews[id].HoldForDelivery();

        if (pendingCount < pendingIds.Length)
        {
            pendingIds[pendingCount] = id;
            pendingDelivered[pendingCount] = delivered;
            pendingCount++;
        }
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

        int customerIndex = round.Orders[id].Customer;
        if (delivered)
        {
            Message("Pedido entregue!");
            AudioManager.Instance?.PlayConfirm();
            MequiHaptics.Confirm();
            orderViews[id].PlayDelivery(
                customers[customerIndex].DeliveryTarget,
                () => BeginCustomerReplacement(id, customerIndex, true));
        }
        else
        {
            Message("A paciência acabou.");
            AudioManager.Instance?.PlayError();
            MequiHaptics.Reject();
            orderViews[id].Clear();
            BeginCustomerReplacement(id, customerIndex, false);
        }
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

                    ActivateOrder(id, incomingCustomer, customerAnchors[0], false);
                    for (int i = 1; i < customerLine.Count; i++)
                        customers[customerLine[i]].MoveTo(customerAnchors[i]);
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
        if (!finalCycle || ending || processingResolution || pendingCount > 0
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
        feedback.text = value;
        feedbackClock = 1.7f;
    }

    private bool SetupError(string detail)
    {
        Debug.LogError($"Rush Balance — {name}: {detail}", this);
        return false;
    }

    private bool ValidateSetup()
    {
        if (session == null) return SetupError("session não atribuído.");
        if (feedback == null) return SetupError("feedback não atribuído.");
        if (finalCycleBanner == null || finalCyclePanel == null || finalCycleGroup == null || finalCycleLabel == null)
            return SetupError("aviso do ciclo final e suas referências.");
        if (customers == null || customers.Length != 5) return SetupError("customers precisa de cinco elementos.");
        if (orderViews == null || orderViews.Length != 4) return SetupError("orderViews precisa de quatro elementos.");
        if (queueAnchors == null || queueAnchors.Length != 4) return SetupError("queueAnchors precisa de quatro elementos.");
        if (queueTargets == null || queueTargets.Length != 4) return SetupError("queueTargets precisa de quatro elementos.");
        if (customerAnchors == null || customerAnchors.Length != 4)
            return SetupError("customerAnchors precisa de quatro elementos.");
        if (customerEntrance == null || customerExit == null) return SetupError("entrada e saída dos clientes.");
        if (replacementPause < 0f || replacementSettleSeconds <= 0f)
            return SetupError("tempos de substituição dos clientes.");
        if (finalNoticeEnterSeconds <= 0f || finalNoticeHoldSeconds < 0f || finalNoticeExitSeconds <= 0f)
            return SetupError("tempos do aviso de ciclo final.");
        if (preparationTimes.x <= 0f || preparationTimes.y <= 0f || preparationTimes.z <= 0f
            || patienceTimes.x <= 0f || patienceTimes.y <= 0f || patienceTimes.z <= 0f
            || secondarySpeed < 0f || secondarySpeed > 1f
            || simulationSpeed <= 0f || animationSpeed <= 0f)
            return SetupError("tempos de preparo, paciência ou velocidade.");

        for (int i = 0; i < customers.Length; i++)
            if (customers[i] == null || !customers[i].Configured)
                return SetupError($"customers[{i}] e suas referências.");
        for (int i = 0; i < orderViews.Length; i++)
        {
            if (orderViews[i] == null || !orderViews[i].Configured)
                return SetupError($"orderViews[{i}] e suas referências.");
            if (queueAnchors[i] == null || queueTargets[i] == null || customerAnchors[i] == null)
                return SetupError($"slot {i}.");
            if (!queueTargets[i].ConfiguredFor(this, i))
                return SetupError($"queueTargets[{i}], controller, rank ou highlight.");
        }
        return true;
    }
}
