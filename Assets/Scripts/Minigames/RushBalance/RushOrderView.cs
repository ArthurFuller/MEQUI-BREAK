using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public sealed class RushOrderView : HierarchyDragHandle, IDropHandler, IPointerClickHandler
{
    [Header("Card montado na Hierarchy")]
    [SerializeField] private GameObject cardVisual;
    [SerializeField] private Image cardBackground;
    [SerializeField] private Sprite[] cardStateSprites;
    [SerializeField] private RectTransform deliveryIconsRoot;
    [SerializeField] private GameObject[] itemIcons;
    [SerializeField] private Image customerBody;
    [SerializeField] private Image customerFace;
    [SerializeField] private Image customerHat;
    [Tooltip("Mesma proporção usada pelo NPC; o card deve ser apenas uma miniatura.")]
    [SerializeField, Min(.01f)] private float hatScaleMultiplier = .78f;
    [SerializeField] private Image progressFill;
    [SerializeField] private RectTransform progressTrack;
    [SerializeField, Range(0f, 6f)] private float progressBarInset = 1f;
    [SerializeField] private Color progressStartColor = new Color32(223, 77, 67, 255);
    [SerializeField] private Color progressEndColor = new Color32(79, 187, 112, 255);
    [SerializeField] private Image priorityIndicator;
    [SerializeField, Min(.05f)] private float deliverySeconds = .36f;
    [SerializeField, Min(0f)] private float itemStagger = .1f;
    [SerializeField, Min(0f)] private float deliveryArcHeight = 90f;
    [SerializeField, Min(.05f)] private float appearanceSeconds = .2f;
    [Header("Tamanho dos cards")][SerializeField, Range(1f, 1.25f)] private float cardSlotScale = 1.1f;
    [SerializeField, Min(1f)] private float barSmoothing = 12f;

    [Header("Feedback no estilo da Customization")]
    [Tooltip("Escala máxima do pulso único ao interagir ou mudar de estado.")]
    [SerializeField, Range(1f, 1.1f)] private float pulseScale = 1.04f;
    [Tooltip("Duração de cada metade do pulso.")]
    [SerializeField, Min(.01f)] private float pulseHalfDuration = .1f;
    [Tooltip("Suaviza a troca da cor que indica a prioridade do pedido.")]
    [SerializeField, Min(.01f)] private float priorityColorDuration = .16f;

    [Header("Estados do card: normal, arrastando, falhou, pronto")]
    [SerializeField] private Color readyCardTint = new Color(.4f, 1f, .48f, 1f);
    [SerializeField, Min(0f)] private float invalidDropShake = 7f;
    [SerializeField, Min(.01f)] private float invalidDropSeconds = .18f;
    [Header("Feedback ao falhar um pedido")]
    [SerializeField, Min(.01f)] private float failureShakeSeconds = .28f;
    [SerializeField, Min(0f)] private float failureShakeStrength = 6f;
    [SerializeField, Min(0f)] private float failureFadeDelay = .12f;
    [SerializeField, Min(.01f)] private float failureFadeSeconds = .2f;
    [SerializeField, Min(1)] private int failureShakeVibrato = 10;
    [Header("Animação de entrega dos itens")]
    [SerializeField] private Ease deliveryPathEase = Ease.InOutCubic;
    [SerializeField] private Ease deliveredItemShrinkEase = Ease.InQuad;
    [SerializeField, Min(0f)] private float deliveryRotation = 180f;

    [Header("Fila suspensa - opcionais, montados na cena")]
    [SerializeField] private RectTransform hangingRoot;
    [SerializeField] private RectTransform selectionRoot;
    [SerializeField] private Image[] highlightBorders;
    [SerializeField] private Image paperOutline;
    [SerializeField] private GameObject completedStamp;
    [SerializeField] private GameObject cancelledStamp;
    [SerializeField] private TMP_Text remainingLabel;
    [SerializeField] private Color readyOutlineColor = new Color(1f, .8f, .12f);
    [SerializeField] private Color selectedOutlineColor = Color.white;
    [SerializeField, Range(1f, 1.1f)] private float selectedScale = 1.04f;
    [SerializeField, Range(0f, 8f)] private float swingDegrees = 3f;
    [SerializeField, Min(.05f)] private float swingSeconds = .42f;
    [SerializeField, Min(0f)] private float entryOffset = 65f;
    [SerializeField, Min(0f)] private float stampHoldSeconds = .22f;
    [SerializeField, Min(.01f)] private float stampLandSeconds = .14f;
    [SerializeField, Min(.01f)] private float stampPulseSeconds = .23f;
    [SerializeField, Range(0f, .1f)] private float stampPulseStrength = .035f;
    private Vector3 completedStampScale, cancelledStampScale;
    [Header("Saida do card pela parte inferior da tela")]
    [SerializeField, Min(.05f)] private float exitSeconds = .35f;
    [SerializeField, Min(0f)] private float exitMargin = 24f;
    [SerializeField] private Ease exitEase = Ease.InCubic;
    private readonly Vector3[] exitCorners = new Vector3[4];
    private Tween swingTween, selectionTween;
    private Vector2 hangingPosition;
    private Vector3 selectionScale;
    private Quaternion hangingRotation;
    private Color paperColor;
    private bool selected;

    private RectTransform previousAnchor;
    private Tween deliveryTween;
    private Tween appearanceTween;
    private Tween cardPulse;
    private Tween priorityTween;
    private Tween invalidDropTween;
    private Vector3 orderSlotBaseScale;
    private Vector3 cardBaseScale;
    private Vector3 cardBasePosition;
    private Vector3 deliveryBaseScale;
    private Vector2 deliveryBasePosition;
    private readonly Vector3[] itemBasePositions = new Vector3[3];
    private readonly Vector3[] itemBaseScales = new Vector3[3];
    private readonly Quaternion[] itemBaseRotations = new Quaternion[3];
    private float progressDisplayed;
    private Image deliveryBox;
    private float progressTarget;
    private bool defaultsCached;
    private bool wasReady;
    private bool delivering;
    private bool failing;
    private bool waitingForResolution;
    private int visibleItemCount;
    private int previousRank = -1;
    private Color priorityTargetColor;
    private bool priorityTargetCached;
    private int visualState = -1;

    public RushBalanceController Owner { get; private set; }
    public int OrderId { get; private set; } = -1;
    public bool IsDelivering => delivering;
    public bool IsFailing => failing;
    public string ConfigurationError =>
        DragConfigurationError != null ? DragConfigurationError :
        cardVisual == null ? "cardVisual" :
        cardBackground == null ? "cardBackground" :
        cardStateSprites == null || cardStateSprites.Length != 4 ? "cardStateSprites" :
        cardStateSprites[0] == null || cardStateSprites[1] == null ||
        cardStateSprites[2] == null || cardStateSprites[3] == null ? "cardStateSprites: sprite ausente" :
        deliveryIconsRoot == null ? "deliveryIconsRoot" :
        customerBody == null ? "customerBody" :
        customerFace == null ? "customerFace" :
        customerHat == null ? "customerHat" :
        progressFill == null ? "progressFill" :
        priorityIndicator == null ? "priorityIndicator" :
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
        {
            if (hangingRoot != null)
            {
                appearanceTween?.Kill();
                hangingRoot.anchoredPosition = hangingPosition;
                StopSwing();
                Owner.SelectQueueCard(OrderId);
            }
            transform.SetAsLastSibling();
            SetVisualState(1);
        }
        else if (!delivering && !failing)
        {
            SetVisualState(wasReady ? 3 : 0);
            if (gameObject.activeInHierarchy && Owner != null && Owner.Rank(OrderId) >= 0)
                Owner.RestoreQueueSiblingOrder(OrderId);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        orderSlotBaseScale = transform.localScale;
        transform.localScale = orderSlotBaseScale * cardSlotScale;
        UpdateDragVisualScaleBaseline();
        CacheDefaults();
    }

    private void CacheDefaults()
    {
        if (defaultsCached || deliveryIconsRoot == null) return;
        deliveryBox = deliveryIconsRoot.GetComponent<Image>();
        progressFill.type = progressTrack != null ? Image.Type.Sliced : Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        SetProgress(0f);
        cardBaseScale = cardVisual.transform.localScale;
        if (completedStamp != null) completedStampScale = completedStamp.transform.localScale;
        if (cancelledStamp != null) cancelledStampScale = cancelledStamp.transform.localScale;
        cardBasePosition = cardVisual.transform.localPosition;
        paperColor = cardBackground.color;
        if (hangingRoot != null)
        {
            hangingPosition = hangingRoot.anchoredPosition;
            hangingRotation = hangingRoot.localRotation;
        }
        if (selectionRoot != null) selectionScale = selectionRoot.localScale;
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
        float blend = 1f - Mathf.Exp(-barSmoothing * Time.unscaledDeltaTime);
        progressDisplayed = progressTarget >= 1f ? 1f : Mathf.Lerp(progressDisplayed, progressTarget, blend);
        SetProgress(progressDisplayed);
    }

    private void SetProgress(float value)
    {
        if (progressTrack == null)
        {
            progressFill.fillAmount = value;
            return;
        }
        RectTransform bar = progressFill.rectTransform;
        float inset = Mathf.Clamp(progressBarInset, 0f, progressTrack.rect.height * .5f);
        float width = Mathf.Max(0f, progressTrack.rect.width - inset * 2f) * Mathf.Clamp01(value);
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f);
        bar.pivot = new Vector2(0f, .5f);
        bar.anchoredPosition = new Vector2(-progressTrack.rect.width * .5f + inset, 0f);
        bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, progressTrack.rect.height - inset * 2f));
        bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        progressFill.enabled = width > .5f;
        progressFill.color = Color.Lerp(progressStartColor, progressEndColor, Mathf.Clamp01(value));
    }

    public void Bind(
        RushBalanceController controller,
        int id,
        int items,
        RushBalanceController.NpcAppearance appearance,
        Sprite faceSprite)
    {
        CacheDefaults();
        ResetQueueFeedback();
        Owner = controller;
        OrderId = id;
        previousAnchor = null;
        wasReady = false;
        delivering = false;
        failing = false;
        waitingForResolution = false;
        deliveryTween?.Kill();
        appearanceTween?.Kill();
        cardPulse?.Kill();
        priorityTween?.Kill();
        invalidDropTween?.Kill();
        visibleItemCount = items;
        previousRank = -1;
        priorityTargetCached = false;
        visualState = -1;
        customerBody.color = appearance.BodyColor;
        customerFace.sprite = faceSprite;
        customerFace.enabled = faceSprite != null;
        customerHat.sprite = appearance.HatSprite;
        customerHat.enabled = appearance.HatSprite != null;
        AvatarView.StyleCustomizationIcon(customerHat, AvatarCustomizationCategory.Hat);
        AvatarView.StyleCustomizationIcon(customerFace, AvatarCustomizationCategory.Face);
        AvatarView.ApplyNormalizedTransform(
            customerHat,
            appearance.HatOffset,
            appearance.HatScale * hatScaleMultiplier,
            appearance.HatRotation);
        deliveryIconsRoot.anchoredPosition = deliveryBasePosition;
        deliveryIconsRoot.localScale = deliveryBaseScale;
        if (deliveryBox != null) deliveryBox.enabled = true;
        cardVisual.transform.localScale = cardBaseScale;
        cardVisual.transform.localPosition = cardBasePosition;
        DragCanvasGroup.alpha = 1f;
        DragCanvasGroup.blocksRaycasts = true;
        progressTarget = 0f;
        progressDisplayed = 0f;
        SetProgress(0f);
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

    public void SetFoodSprites(Sprite[] sprites)
    {
        for (int i = 0; i < sprites.Length && i < itemIcons.Length; i++)
        {
            Image image = itemIcons[i].GetComponentInChildren<Image>(true);
            if (image != null) image.sprite = sprites[i];
        }
    }

    public void Clear()
    {
        ResetQueueFeedback();
        deliveryTween?.Kill();
        appearanceTween?.Kill();
        cardPulse?.Kill();
        priorityTween?.Kill();
        invalidDropTween?.Kill();
        delivering = false;
        failing = false;
        waitingForResolution = false;
        wasReady = false;
        previousAnchor = null;
        visibleItemCount = 0;
        previousRank = -1;
        priorityTargetCached = false;
        visualState = -1;
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
        if (deliveryBox != null) deliveryBox.enabled = true;
        progressTarget = 0f;
        if (progressFill != null) SetProgress(0f);
        progressDisplayed = 0f;
        gameObject.SetActive(false);
    }

    public void Refresh(RushRound.Order order, Color bodyColor, Sprite faceSprite)
    {
        // Preserva o card na fila visual ate a entrega ou falha terminar.
        if (delivering || failing || waitingForResolution) return;
        bool visible = order.State == RushRound.Status.Queued || order.State == RushRound.Status.Ready;
        if (!visible)
        {
            gameObject.SetActive(false);
            return;
        }

        bool wasVisible = gameObject.activeSelf;
        // A resolução segura cards novos; os que já estavam na fila podem avançar.
        if (Owner.HoldQueuePositions && !wasVisible) return;
        gameObject.SetActive(true);
        cardVisual.SetActive(true);
        customerBody.color = bodyColor;
        customerFace.sprite = faceSprite;
        customerFace.enabled = faceSprite != null;
        progressTarget = Mathf.Clamp01(order.Work / order.Duration);
        if (remainingLabel != null)
            remainingLabel.SetText("{0:0}s", Mathf.Ceil(Mathf.Max(0f, order.Duration - order.Work)));

        int rank = Owner.Rank(OrderId);
        bool ready = order.State == RushRound.Status.Ready;
        // Mantém posição e prioridade visual estáveis durante a saída de outro card.
        if (Owner.HoldQueuePositions && wasVisible)
        {
            if (!IsDragging) SetVisualState(ready ? 3 : 0);
            if (ready && !wasReady)
            {
                AudioManager.Instance?.PlayReady();
                MequiHaptics.Confirm();
                PlaySinglePulse();
            }
            wasReady = ready;
            return;
        }
        Color nextPriorityColor = ready
            ? new Color(.34f, .72f, .39f)
            : rank == 0 ? new Color(1f, .73f, .08f)
            : rank == 1 ? new Color(.95f, .48f, .16f) : new Color(.35f, .38f, .4f);
        SetPriorityColor(nextPriorityColor, wasVisible);
        if (!IsDragging) SetVisualState(ready ? 3 : 0);

        int oldRank = previousRank;
        bool queuePositionChanged = wasVisible && oldRank >= 0 && rank != oldRank;
        if (queuePositionChanged && (hangingRoot != null || rank < 2))
            PlaySinglePulse();
        previousRank = rank;

        if (ready && !wasReady)
        {
            AudioManager.Instance?.PlayReady();
            MequiHaptics.Confirm();
            PlaySinglePulse();
        }
        wasReady = ready;

        RectTransform anchor = Owner.Anchor(OrderId);
        if (anchor != previousAnchor || !wasVisible)
        {
            previousAnchor = anchor;
            if (!wasVisible) MoveHome(true);
            else if (queuePositionChanged)
                MoveHome(Owner.QueueCardMoveSeconds, Owner.QueueCardMoveEase);
            else MoveHome(false);
        }
        if (!wasVisible) PlayAppearance();
    }

    private void PlayAppearance()
    {
        appearanceTween?.Kill();
        DragCanvasGroup.alpha = 0f;
        if (hangingRoot != null)
        {
            hangingRoot.anchoredPosition = hangingPosition + Vector2.down * entryOffset;
            appearanceTween = DOTween.Sequence().SetTarget(this)
                .Join(DragCanvasGroup.DOFade(1f, Seconds(appearanceSeconds)))
                .Join(hangingRoot.DOAnchorPos(hangingPosition, Seconds(appearanceSeconds)).SetEase(Ease.OutCubic));
            return;
        }
        cardVisual.transform.localScale = cardBaseScale * .92f;
        appearanceTween = DOTween.Sequence()
            .SetTarget(this)
            .Join(DragCanvasGroup.DOFade(1f, Seconds(appearanceSeconds)).SetEase(Ease.OutQuad))
            .Join(cardVisual.transform.DOScale(cardBaseScale, Seconds(appearanceSeconds)).SetEase(Ease.OutBack));
    }

    private void SetVisualState(int nextState)
    {
        if (visualState == nextState) return;
        visualState = nextState;
        if (hangingRoot != null)
        {
            cardBackground.color = paperColor;
            UpdateOutline();
            return;
        }
        cardBackground.sprite = cardStateSprites[nextState];
        cardBackground.color = nextState == 3 ? readyCardTint : Color.white;
    }

    public void PlayFailure(Action completed)
    {
        waitingForResolution = false;
        if (!gameObject.activeSelf)
        {
            completed?.Invoke();
            return;
        }

        CancelDrag(true);
        failing = true;
        appearanceTween?.Kill();
        StopSwing();
        SetSelected(false);
        if (hangingRoot != null) hangingRoot.anchoredPosition = hangingPosition;
        cardPulse?.Kill();
        invalidDropTween?.Kill();
        cardVisual.SetActive(true);
        cardVisual.transform.localScale = cardBaseScale;
        cardVisual.transform.localPosition = cardBasePosition;
        DragCanvasGroup.alpha = 1f;
        DragCanvasGroup.blocksRaycasts = false;
        SetVisualState(2);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        if (cancelledStamp != null)
            sequence.Append(CreateStampFeedback(cancelledStamp, false))
                .AppendInterval(Seconds(Mathf.Max(0f, stampHoldSeconds - stampLandSeconds - stampPulseSeconds)));
        else
        {
            Owner?.PlayStampSound(false);
            sequence.Append(cardVisual.transform.DOShakePosition(Seconds(failureShakeSeconds), failureShakeStrength, failureShakeVibrato, 0f, false, true));
        }
        sequence.AppendInterval(Seconds(failureFadeDelay));
        if (hangingRoot != null) sequence.Append(CreateExitTween());
        else sequence.Append(DragCanvasGroup.DOFade(0f, Seconds(failureFadeSeconds)))
            .Join(cardVisual.transform.DOScale(cardBaseScale * .78f, Seconds(failureFadeSeconds)).SetEase(Ease.InCubic));
        invalidDropTween = sequence.OnComplete(() =>
            {
                Clear();
                completed?.Invoke();
            });
    }

    private void PlaySinglePulse()
    {
        if (cardVisual == null || delivering || IsDragging) return;
        if (hangingRoot != null)
        {
            StopSwing();
            swingTween = hangingRoot.DOPunchRotation(Vector3.forward * swingDegrees,
                Seconds(swingSeconds), 3, .35f).SetTarget(this);
            return;
        }

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
        waitingForResolution = false;
        delivering = true;
        CancelDrag(true);
        appearanceTween?.Kill();
        cardPulse?.Kill();
        DragCanvasGroup.alpha = 1f;
        DragCanvasGroup.blocksRaycasts = false;
        StopSwing();
        if (hangingRoot != null) hangingRoot.anchoredPosition = hangingPosition;
        SetSelected(false);
        cardVisual.SetActive(hangingRoot != null);
        cardVisual.transform.localScale = cardBaseScale;
        cardVisual.transform.localPosition = cardBasePosition;
        if (deliveryBox != null) deliveryBox.enabled = false;
        deliveryIconsRoot.gameObject.SetActive(true);
        deliveryTween?.Kill();
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        float stampDelay = hangingRoot != null ? Seconds(stampHoldSeconds) : 0f;
        if (completedStamp != null)
        {
            sequence.Append(CreateStampFeedback(completedStamp, true));
            stampDelay = Mathf.Max(stampDelay, sequence.Duration());
        }
        else Owner?.PlayStampSound(true);
        for (int i = 0; i < visibleItemCount; i++)
        {
            Transform icon = itemIcons[i].transform;
            Vector3 start = icon.position;
            float delay = stampDelay + i * Seconds(itemStagger);
            sequence.Insert(delay, DOTween.To(
                () => 0f,
                value =>
                {
                    Vector3 end = target.position;
                    Vector3 control = (start + end) * .5f + Vector3.up * deliveryArcHeight;
                    icon.position = EvaluateBezier(start, control, end, value);
                },
                1f,
                Seconds(deliverySeconds)).SetEase(deliveryPathEase));
            sequence.Insert(delay, icon.DOScale(itemBaseScales[i] * .7f, Seconds(deliverySeconds)).SetEase(deliveredItemShrinkEase));
            sequence.Insert(delay, icon.DORotate(new Vector3(0f, 0f, deliveryRotation), Seconds(deliverySeconds), RotateMode.FastBeyond360));
        }
        if (hangingRoot != null)
        {
            sequence.AppendCallback(() =>
            {
                // Os itens ja chegaram ao cliente; nao devem descer junto com o papel.
                foreach (GameObject icon in itemIcons) icon.SetActive(false);
            });
            sequence.Append(CreateExitTween());
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

    private Sequence CreateStampFeedback(GameObject stamp, bool delivered)
    {
        Vector3 stampScale = delivered ? completedStampScale : cancelledStampScale;
        stamp.SetActive(true);
        stamp.transform.localScale = stampScale * 1.22f;
        cardVisual.transform.localScale = cardBaseScale;
        return DOTween.Sequence().SetTarget(this)
            .Append(stamp.transform.DOScale(stampScale, Seconds(stampLandSeconds)).SetEase(Ease.OutCubic))
            .AppendCallback(() => Owner?.PlayStampSound(delivered))
            .Append(cardVisual.transform.DOScale(cardBaseScale * (1f - stampPulseStrength),
                Seconds(stampPulseSeconds * .3f)).SetEase(Ease.OutQuad))
            .Append(cardVisual.transform.DOScale(cardBaseScale,
                Seconds(stampPulseSeconds * .7f)).SetEase(Ease.InOutSine));
    }

    private Tween CreateExitTween()
    {
        // Usa os limites do Canvas para retirar todo o papel da tela em qualquer proporcao.
        Canvas canvas = hangingRoot.GetComponentInParent<Canvas>();
        RectTransform screen = (RectTransform)canvas.rootCanvas.transform;
        screen.GetWorldCorners(exitCorners);
        float bottom = Mathf.Min(exitCorners[0].y, exitCorners[3].y);
        hangingRoot.GetWorldCorners(exitCorners);
        float top = Mathf.Max(exitCorners[1].y, exitCorners[2].y);
        float margin = screen.TransformVector(Vector3.up * exitMargin).magnitude;
        float distance = Mathf.Max(0f, top - bottom) + margin;
        return hangingRoot.DOMoveY(hangingRoot.position.y - distance, Seconds(exitSeconds))
            .SetEase(exitEase).SetTarget(this);
    }

    public void HoldForResolution()
    {
        if (delivering) return;
        waitingForResolution = true;
        // Um card à espera da animação não pode bloquear o toque em outro
        // pedido que já está pronto, mesmo quando os dois se sobrepõem.
        DragCanvasGroup.blocksRaycasts = false;
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
        if (eventData.button != PointerEventData.InputButton.Left || Owner == null || delivering || failing
            || waitingForResolution || IsDragging || Time.unscaledTime - LastDragEndTime < .15f) return;
        if (Owner.QueueControlsEnabled && !Owner.IsReady(OrderId))
        {
            Owner.SelectQueueCard(OrderId);
            return;
        }
        if (!Owner.IsReady(OrderId)) PlaySinglePulse();
        Owner.Deliver(OrderId);
    }

    protected override void DragFinished(bool accepted)
    {
        if (accepted && Owner != null)
        {
            MoveHome(Owner.QueueCardMoveSeconds, Owner.QueueCardMoveEase);
            PlaySinglePulse();
            return;
        }
        if (accepted || cardVisual == null || invalidDropShake <= 0f) return;
        invalidDropTween?.Kill();
        cardVisual.transform.localPosition = cardBasePosition;
        invalidDropTween = cardVisual.transform
            .DOShakePosition(Seconds(invalidDropSeconds), invalidDropShake, 12, 0f, false, true)
            .SetTarget(this)
            .OnComplete(() =>
            {
                cardVisual.transform.localPosition = cardBasePosition;
            });
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
        ResetQueueFeedback();
        base.OnDisable();
        appearanceTween?.Kill();
        cardPulse?.Kill();
        priorityTween?.Kill();
        invalidDropTween?.Kill();
        if (!delivering) deliveryTween?.Kill();
        if (cardVisual != null && defaultsCached)
        {
            cardVisual.transform.localScale = cardBaseScale;
            cardVisual.transform.localPosition = cardBasePosition;
        }
        if (DragCanvasGroup != null) DragCanvasGroup.alpha = 1f;
    }

    public void SetSelected(bool value)
    {
        if (selectionRoot == null || selected == value) return;
        selected = value;
        selectionTween?.Kill();
        Vector3 target = selectionScale * (value ? selectedScale : 1f);
        if (!gameObject.activeInHierarchy) selectionRoot.localScale = target;
        else selectionTween = selectionRoot.DOScale(target, Seconds(.12f)).SetEase(Ease.OutQuad).SetTarget(this);
        UpdateOutline();
    }

    private void UpdateOutline()
    {
        if (paperOutline != null)
        {
            paperOutline.color = visualState == 3 ? readyOutlineColor : selectedOutlineColor;
            paperOutline.enabled = visualState == 3 || selected;
        }
        if (highlightBorders == null) return;
        foreach (Image edge in highlightBorders)
        {
            if (edge == null) continue;
            edge.enabled = paperOutline == null && (visualState == 3 || selected);
            edge.color = visualState == 3 ? readyOutlineColor : selectedOutlineColor;
        }
    }

    private void StopSwing()
    {
        swingTween?.Kill();
        if (hangingRoot != null && defaultsCached) hangingRoot.localRotation = hangingRotation;
    }

    private void ResetQueueFeedback()
    {
        if (paperOutline != null) paperOutline.enabled = false;
        StopSwing();
        selectionTween?.Kill();
        selected = false;
        if (defaultsCached && hangingRoot != null)
        {
            hangingRoot.anchoredPosition = hangingPosition;
        }
        if (defaultsCached && selectionRoot != null) selectionRoot.localScale = selectionScale;
        if (completedStamp != null)
        {
            completedStamp.SetActive(false);
            if (defaultsCached) completedStamp.transform.localScale = completedStampScale;
        }
        if (cancelledStamp != null)
        {
            cancelledStamp.SetActive(false);
            if (defaultsCached) cancelledStamp.transform.localScale = cancelledStampScale;
        }
        if (highlightBorders != null)
            foreach (Image edge in highlightBorders)
                if (edge != null) edge.enabled = false;
    }
}
