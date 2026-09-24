using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RushOrderView : HierarchyDragHandle, IDropHandler, IPointerClickHandler
{
    [Header("Card montado na Hierarchy")]
    [SerializeField] private GameObject cardVisual;
    [SerializeField] private Image cardBackground;
    [SerializeField] private RectTransform deliveryIconsRoot;
    [SerializeField] private GameObject[] itemIcons;
    [SerializeField] private Image customerBody;
    [SerializeField] private Image customerFace;
    [SerializeField] private Image customerHat;
    [Tooltip("Mesma proporção usada pelo NPC; o card deve ser apenas uma miniatura.")]
    [SerializeField, Min(.01f)] private float hatScaleMultiplier = .78f;
    [SerializeField] private Image progressFill;
    [SerializeField] private Image priorityIndicator;
    [SerializeField] private GameObject readyHighlight;
    [SerializeField, Min(.05f)] private float deliverySeconds = .36f;
    [SerializeField, Min(0f)] private float itemStagger = .1f;
    [SerializeField, Min(0f)] private float deliveryArcHeight = 90f;
    [SerializeField, Min(.05f)] private float appearanceSeconds = .2f;
    [SerializeField, Min(1f)] private float barSmoothing = 12f;

    [Header("Feedback no estilo da Customization")]
    [Tooltip("Escala máxima do pulso único ao interagir ou mudar de estado.")]
    [SerializeField, Range(1f, 1.1f)] private float pulseScale = 1.04f;
    [Tooltip("Duração de cada metade do pulso.")]
    [SerializeField, Min(.01f)] private float pulseHalfDuration = .1f;
    [Tooltip("Suaviza a troca da cor que indica a prioridade do pedido.")]
    [SerializeField, Min(.01f)] private float priorityColorDuration = .16f;

    [Header("Estados e affordances")]
    [SerializeField] private Color activeColor = new Color(.18f, .17f, .12f, 1f);
    [SerializeField] private Color secondaryColor = new Color(.16f, .14f, .12f, 1f);
    [SerializeField] private Color waitingColor = new Color(.12f, .12f, .12f, 1f);
    [SerializeField] private Color readyColor = new Color(.12f, .2f, .14f, 1f);
    [SerializeField, Range(.4f, 1f)] private float secondaryAlpha = 1f;
    [SerializeField, Range(.4f, 1f)] private float waitingAlpha = 1f;
    [SerializeField, Min(.01f)] private float stateTransitionSeconds = .16f;
    [SerializeField, Min(0f)] private float invalidDropShake = 7f;
    [SerializeField, Min(.01f)] private float invalidDropSeconds = .18f;
    [SerializeField, Min(0f)] private float readyPunch = .065f;
    [SerializeField, Min(.01f)] private float readyPunchSeconds = .22f;
    [SerializeField, Min(0f)] private float deliveryRotation = 180f;

    private RectTransform previousAnchor;
    private Tween readyPulse;
    private Tween deliveryTween;
    private Tween appearanceTween;
    private Tween cardPulse;
    private Tween priorityTween;
    private Tween stateTween;
    private Tween invalidDropTween;
    private Vector3 readyBaseScale;
    private Vector3 cardBaseScale;
    private Vector3 cardBasePosition;
    private Vector3 deliveryBaseScale;
    private Vector2 deliveryBasePosition;
    private readonly Vector3[] itemBasePositions = new Vector3[3];
    private readonly Vector3[] itemBaseScales = new Vector3[3];
    private readonly Quaternion[] itemBaseRotations = new Quaternion[3];
    private Vector3 progressFullScale;
    private RectTransform progressBar;
    private float progressTarget;
    private bool defaultsCached;
    private bool wasReady;
    private bool delivering;
    private bool waitingForDelivery;
    private int visibleItemCount;
    private int previousRank = -1;
    private Color priorityTargetColor;
    private bool priorityTargetCached;
    private int visualState = -1;
    private float stateAlphaTarget = 1f;

    public RushBalanceController Owner { get; private set; }
    public int OrderId { get; private set; } = -1;
    public string ConfigurationError =>
        DragConfigurationError != null ? DragConfigurationError :
        cardVisual == null ? "cardVisual" :
        cardBackground == null ? "cardBackground" :
        deliveryIconsRoot == null ? "deliveryIconsRoot" :
        customerBody == null ? "customerBody" :
        customerFace == null ? "customerFace" :
        customerHat == null ? "customerHat" :
        progressFill == null ? "progressFill" :
        priorityIndicator == null ? "priorityIndicator" :
        readyHighlight == null ? "readyHighlight" :
        itemIcons == null || itemIcons.Length != 3 ? "itemIcons: esperado 3 elementos" :
        itemIcons[0] == null ? "itemIcons[0]" :
        itemIcons[1] == null ? "itemIcons[1]" :
        itemIcons[2] == null ? "itemIcons[2]" :
        null;
    public bool Configured => ConfigurationError == null;
    protected override bool CanDrag => !delivering && Owner != null && Owner.CanDrag(OrderId);
    protected override Vector3 RestPosition => Owner != null && Owner.Anchor(OrderId) != null
        ? Owner.Anchor(OrderId).position : transform.position;

    protected override void DragStateChanged(bool dragging)
    {
        // Todos os cards já pertencem à CamadaCards na Hierarchy. Colocar apenas
        // o card ativo como último irmão garante que ele seja renderizado acima
        // dos demais sem trocar de pai ou criar qualquer objeto em runtime.
        if (dragging)
            transform.SetAsLastSibling();
    }

    protected override void Awake()
    {
        base.Awake();
        CacheDefaults();
    }

    private void CacheDefaults()
    {
        if (defaultsCached || readyHighlight == null || deliveryIconsRoot == null) return;
        progressBar = progressFill.rectTransform;
        progressFullScale = progressBar.localScale;
        progressFill.type = Image.Type.Simple;
        readyBaseScale = readyHighlight.transform.localScale;
        cardBaseScale = cardVisual.transform.localScale;
        cardBasePosition = cardVisual.transform.localPosition;
        deliveryBaseScale = deliveryIconsRoot.localScale;
        deliveryBasePosition = deliveryIconsRoot.anchoredPosition;
        for (int i = 0; i < itemIcons.Length; i++)
        {
            itemBasePositions[i] = itemIcons[i].transform.localPosition;
            itemBaseScales[i] = itemIcons[i].transform.localScale;
            itemBaseRotations[i] = itemIcons[i].transform.localRotation;
        }
        defaultsCached = true;
    }

    private void Update()
    {
        if (progressBar == null) return;
        float blend = 1f - Mathf.Exp(-barSmoothing * Time.unscaledDeltaTime);
        Vector3 scale = progressBar.localScale;
        scale.x = Mathf.Lerp(scale.x, progressFullScale.x * progressTarget, blend);
        progressBar.localScale = scale;
    }

    public void Bind(
        RushBalanceController controller,
        int id,
        int items,
        RushBalanceController.NpcAppearance appearance,
        Sprite faceSprite)
    {
        CacheDefaults();
        Owner = controller;
        OrderId = id;
        previousAnchor = null;
        wasReady = false;
        delivering = false;
        waitingForDelivery = false;
        readyPulse?.Kill();
        deliveryTween?.Kill();
        appearanceTween?.Kill();
        cardPulse?.Kill();
        priorityTween?.Kill();
        stateTween?.Kill();
        invalidDropTween?.Kill();
        visibleItemCount = items;
        previousRank = -1;
        priorityTargetCached = false;
        visualState = -1;
        stateAlphaTarget = 1f;
        customerBody.color = appearance.BodyColor;
        customerFace.sprite = faceSprite;
        customerFace.enabled = faceSprite != null;
        customerHat.sprite = appearance.HatSprite;
        customerHat.enabled = appearance.HatSprite != null;
        AvatarView.ApplyNormalizedTransform(
            customerHat,
            appearance.HatOffset,
            appearance.HatScale * hatScaleMultiplier,
            appearance.HatRotation);
        deliveryIconsRoot.anchoredPosition = deliveryBasePosition;
        deliveryIconsRoot.localScale = deliveryBaseScale;
        cardVisual.transform.localScale = cardBaseScale;
        cardVisual.transform.localPosition = cardBasePosition;
        DragCanvasGroup.alpha = 1f;
        progressTarget = 0f;
        Vector3 emptyScale = progressFullScale;
        emptyScale.x = 0f;
        progressBar.localScale = emptyScale;
        for (int i = 0; i < itemIcons.Length; i++)
        {
            itemIcons[i].transform.localPosition = itemBasePositions[i];
            itemIcons[i].transform.localScale = itemBaseScales[i];
            itemIcons[i].transform.localRotation = itemBaseRotations[i];
            itemIcons[i].SetActive(i < items);
        }
        cardVisual.SetActive(true);
        gameObject.SetActive(false);
    }

    public void Clear()
    {
        readyPulse?.Kill();
        deliveryTween?.Kill();
        appearanceTween?.Kill();
        cardPulse?.Kill();
        priorityTween?.Kill();
        stateTween?.Kill();
        invalidDropTween?.Kill();
        delivering = false;
        waitingForDelivery = false;
        wasReady = false;
        previousAnchor = null;
        visibleItemCount = 0;
        previousRank = -1;
        priorityTargetCached = false;
        visualState = -1;
        stateAlphaTarget = 1f;
        if (cardVisual != null && defaultsCached) cardVisual.transform.localScale = cardBaseScale;
        if (cardVisual != null && defaultsCached) cardVisual.transform.localPosition = cardBasePosition;
        if (DragCanvasGroup != null) DragCanvasGroup.alpha = 1f;
        if (defaultsCached)
        {
            for (int i = 0; i < itemIcons.Length; i++)
            {
                itemIcons[i].transform.localPosition = itemBasePositions[i];
                itemIcons[i].transform.localScale = itemBaseScales[i];
                itemIcons[i].transform.localRotation = itemBaseRotations[i];
            }
        }
        if (deliveryIconsRoot != null && defaultsCached)
        {
            deliveryIconsRoot.anchoredPosition = deliveryBasePosition;
            deliveryIconsRoot.localScale = deliveryBaseScale;
        }
        progressTarget = 0f;
        if (progressBar != null && defaultsCached)
        {
            Vector3 emptyScale = progressFullScale;
            emptyScale.x = 0f;
            progressBar.localScale = emptyScale;
        }
        if (readyHighlight != null) readyHighlight.SetActive(false);
        gameObject.SetActive(false);
    }

    public void Refresh(RushRound.Order order, Color bodyColor, Sprite faceSprite)
    {
        // Um segundo pedido pode ser entregue enquanto a animação anterior ainda
        // está em andamento. Nesse intervalo o estado lógico já é Delivered, mas
        // o card precisa continuar visível até chegar sua vez na fila visual.
        if (delivering || waitingForDelivery) return;
        bool visible = order.State == RushRound.Status.Queued || order.State == RushRound.Status.Ready;
        if (!visible)
        {
            gameObject.SetActive(false);
            return;
        }

        bool wasVisible = gameObject.activeSelf;
        gameObject.SetActive(true);
        cardVisual.SetActive(true);
        customerBody.color = bodyColor;
        customerFace.sprite = faceSprite;
        customerFace.enabled = faceSprite != null;
        progressTarget = Mathf.Clamp01(order.Work / order.Duration);

        int rank = Owner.Rank(OrderId);
        bool ready = order.State == RushRound.Status.Ready;
        Color nextPriorityColor = ready
            ? new Color(.34f, .72f, .39f)
            : rank == 0 ? new Color(1f, .73f, .08f)
            : rank == 1 ? new Color(.95f, .48f, .16f) : new Color(.35f, .38f, .4f);
        SetPriorityColor(nextPriorityColor, wasVisible);
        SetVisualState(ready ? 3 : rank <= 0 ? 0 : rank == 1 ? 1 : 2, wasVisible);

        if (wasVisible && previousRank >= 0 && rank != previousRank && rank < 2)
            PlaySinglePulse();
        previousRank = rank;

        readyHighlight.SetActive(ready);
        if (ready && !wasReady)
        {
            AudioManager.Instance?.PlayReady();
            MequiHaptics.Confirm();
            readyPulse?.Kill();
            readyHighlight.transform.localScale = readyBaseScale;
            readyPulse = readyHighlight.transform
                .DOPunchScale(Vector3.one * readyPunch, Seconds(readyPunchSeconds), 2, .3f)
                .OnComplete(() => readyHighlight.transform.localScale = readyBaseScale);
            PlaySinglePulse();
        }
        wasReady = ready;

        RectTransform anchor = Owner.Anchor(OrderId);
        if (anchor != previousAnchor || !wasVisible)
        {
            previousAnchor = anchor;
            MoveHome(!wasVisible);
        }
        if (!wasVisible) PlayAppearance();
    }

    private void PlayAppearance()
    {
        appearanceTween?.Kill();
        DragCanvasGroup.alpha = 0f;
        cardVisual.transform.localScale = cardBaseScale * .92f;
        appearanceTween = DOTween.Sequence()
            .SetTarget(this)
            .Join(DragCanvasGroup.DOFade(stateAlphaTarget, Seconds(appearanceSeconds)).SetEase(Ease.OutQuad))
            .Join(cardVisual.transform.DOScale(cardBaseScale, Seconds(appearanceSeconds)).SetEase(Ease.OutBack));
    }

    private void SetVisualState(int nextState, bool animate)
    {
        if (visualState == nextState) return;
        visualState = nextState;
        stateAlphaTarget = nextState == 1 ? secondaryAlpha : nextState == 2 ? waitingAlpha : 1f;
        Color targetColor = nextState == 0 ? activeColor
            : nextState == 1 ? secondaryColor : nextState == 2 ? waitingColor : readyColor;

        stateTween?.Kill();
        if (!animate)
        {
            DragCanvasGroup.alpha = stateAlphaTarget;
            cardBackground.color = targetColor;
            return;
        }

        stateTween = DOTween.Sequence().SetTarget(this)
            .Join(DragCanvasGroup.DOFade(stateAlphaTarget, Seconds(stateTransitionSeconds)).SetEase(Ease.OutQuad))
            .Join(cardBackground.DOColor(targetColor, Seconds(stateTransitionSeconds)).SetEase(Ease.OutSine));
    }

    private void PlaySinglePulse()
    {
        if (cardVisual == null || delivering || IsDragging) return;

        cardPulse?.Kill();
        cardVisual.transform.localScale = cardBaseScale;
        cardPulse = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(UpdateType.Late)
            .Append(cardVisual.transform
                .DOScale(cardBaseScale * pulseScale, Seconds(pulseHalfDuration))
                .SetEase(Ease.OutSine))
            .Append(cardVisual.transform
                .DOScale(cardBaseScale, Seconds(pulseHalfDuration))
                .SetEase(Ease.InOutSine))
            .OnComplete(() => cardVisual.transform.localScale = cardBaseScale);
    }

    private void SetPriorityColor(Color target, bool animate)
    {
        if (priorityTargetCached && priorityTargetColor == target) return;

        priorityTargetCached = true;
        priorityTargetColor = target;
        priorityTween?.Kill();

        if (!animate)
        {
            priorityIndicator.color = target;
            return;
        }

        priorityTween = priorityIndicator
            .DOColor(target, Seconds(priorityColorDuration))
            .SetEase(Ease.OutSine)
            .SetTarget(this);
    }

    public void PlayDelivery(RectTransform target, Action completed)
    {
        if (delivering || target == null) return;
        waitingForDelivery = false;
        delivering = true;
        CancelDrag(true);
        readyPulse?.Kill();
        cardVisual.SetActive(false);
        deliveryIconsRoot.gameObject.SetActive(true);
        deliveryTween?.Kill();
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        for (int i = 0; i < visibleItemCount; i++)
        {
            Transform icon = itemIcons[i].transform;
            Vector3 start = icon.position;
            Vector3 end = target.position;
            Vector3 control = (start + end) * .5f + Vector3.up * deliveryArcHeight;
            float delay = i * Seconds(itemStagger);
            sequence.Insert(delay, DOTween.To(
                () => 0f,
                value => icon.position = EvaluateBezier(start, control, end, value),
                1f,
                Seconds(deliverySeconds)).SetEase(Ease.InOutCubic));
            sequence.Insert(delay, icon.DOScale(itemBaseScales[i] * .7f, Seconds(deliverySeconds)).SetEase(Ease.InQuad));
            sequence.Insert(delay, icon.DORotate(new Vector3(0f, 0f, deliveryRotation), Seconds(deliverySeconds), RotateMode.FastBeyond360));
        }
        deliveryTween = sequence
            .OnComplete(() =>
            {
                deliveryTween = null;
                deliveryIconsRoot.anchoredPosition = deliveryBasePosition;
                deliveryIconsRoot.localScale = deliveryBaseScale;
                for (int i = 0; i < itemIcons.Length; i++)
                {
                    itemIcons[i].transform.localPosition = itemBasePositions[i];
                    itemIcons[i].transform.localScale = itemBaseScales[i];
                    itemIcons[i].transform.localRotation = itemBaseRotations[i];
                }
                delivering = false;
                gameObject.SetActive(false);
                completed?.Invoke();
            });
    }

    public void HoldForDelivery()
    {
        if (!delivering) waitingForDelivery = true;
    }

    private static Vector3 EvaluateBezier(Vector3 start, Vector3 control, Vector3 end, float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private float Seconds(float duration)
    {
        return Owner != null ? Owner.AnimationSeconds(duration) : duration;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Owner == null || delivering || Time.unscaledTime - LastDragEndTime < .15f) return;
        if (!Owner.IsReady(OrderId)) PlaySinglePulse();
        Owner.Deliver(OrderId);
    }

    public void AcceptCurrentDrop() => MarkDropAccepted();

    protected override void DragFinished(bool accepted)
    {
        if (accepted || cardVisual == null || invalidDropShake <= 0f) return;
        invalidDropTween?.Kill();
        cardVisual.transform.localPosition = cardBasePosition;
        invalidDropTween = cardVisual.transform
            .DOShakePosition(Seconds(invalidDropSeconds), invalidDropShake, 12, 0f, false, true)
            .SetTarget(this)
            .OnComplete(() => cardVisual.transform.localPosition = cardBasePosition);
        AudioManager.Instance?.PlayError();
        MequiHaptics.Reject();
    }

    protected override void HighlightTargets(bool value)
    {
        if (Owner != null) Owner.Highlight(OrderId, value);
    }

    public void OnDrop(PointerEventData data)
    {
        if (Owner == null || data.pointerDrag == null) return;
        RushOrderView dragged = data.pointerDrag.GetComponent<RushOrderView>();
        if (dragged != null && dragged != this && dragged.Owner == Owner && dragged.OwnsPointer(data))
        {
            dragged.AcceptCurrentDrop();
            Owner.ReorderOnto(dragged.OrderId, OrderId);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        readyPulse?.Kill();
        appearanceTween?.Kill();
        cardPulse?.Kill();
        priorityTween?.Kill();
        stateTween?.Kill();
        invalidDropTween?.Kill();
        if (!delivering) deliveryTween?.Kill();
        if (readyHighlight != null && defaultsCached)
            readyHighlight.transform.localScale = readyBaseScale;
        if (cardVisual != null && defaultsCached)
        {
            cardVisual.transform.localScale = cardBaseScale;
            cardVisual.transform.localPosition = cardBasePosition;
        }
        if (DragCanvasGroup != null) DragCanvasGroup.alpha = 1f;
    }
}
