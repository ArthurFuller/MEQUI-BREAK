using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;

/// <summary>Card fixo na prancheta, com rosto e duas funções configurados na Hierarchy.</summary>
public sealed class ComboCrewWorkerCard : HierarchyDragHandle, IPointerClickHandler
{
    [SerializeField] private ComboCrewController owner;
    [SerializeField, Min(0)] private int workerIndex;
    [SerializeField] private GameObject portraitWhileDragging;
    [SerializeField] private GameObject cardDetails;
    [SerializeField] private ComboCrewClipboard clipboard;
    [SerializeField] private TMP_Text traitLabel;
    [SerializeField] private Sprite positiveBadge;
    [SerializeField] private Sprite negativeBadge;
    [SerializeField] private Sprite neutralBadge;
    [SerializeField, Min(.1f)] private float idleNudgeDelay = 7f;
    private float idleTime;
    private Tween idleNudge;
    private Quaternion nudgeOriginRotation;
    private bool assigned;
    private GameObject assignedOverlay;
    private RectTransform badgeArea;
    private Vector2 boardSize;
    private Tween sizeTween;
    private int hoveredStation = -1;
    public int WorkerIndex => workerIndex;
    public void SetWorkerIndex(int index) => workerIndex = index;
    public bool Configured => DragConfigured && owner != null && clipboard != null
        && portraitWhileDragging != null && cardDetails != null;
    protected override bool CanDrag => owner != null && owner.CanMoveWorker(workerIndex);
    private void Start()
    {
        if (owner == null) return;
        RefreshPortrait();
        TMP_Text nameLabel = cardDetails != null
            ? cardDetails.transform.Find("Funcoes")?.GetComponent<TMP_Text>() : null;
        if (nameLabel != null)
        {
            nameLabel.text = owner.WorkerName(workerIndex).ToUpperInvariant();
            RectTransform nameRect = nameLabel.rectTransform;
            nameRect.anchoredPosition = new Vector2(0f, -29f);
            nameRect.sizeDelta = new Vector2(DragVisual.rect.width - 14f, 36f);
            nameLabel.fontSize = 21f;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 17f;
            nameLabel.fontSizeMax = 21f;
            nameLabel.color = Color.white;
            nameLabel.fontStyle = FontStyles.Bold;
        }
        if (traitLabel != null) traitLabel.gameObject.SetActive(false);
        BuildBadges();
        Transform info = DragVisual != null ? DragVisual.Find("IconeInformacao") : null;
        if (info != null) info.SetAsLastSibling();
    }

    private void BuildBadges()
    {
        if (cardDetails == null) return;
        if (positiveBadge == null)
            positiveBadge = Resources.Load<Sprite>("ComboCrew/Badges/trait_badge_positive");
        if (negativeBadge == null)
            negativeBadge = Resources.Load<Sprite>("ComboCrew/Badges/trait_badge_negative");
        if (neutralBadge == null)
            neutralBadge = Resources.Load<Sprite>("ComboCrew/Badges/trait_badge_neutral");
        if (positiveBadge == null || negativeBadge == null || neutralBadge == null)
            Debug.LogError("Combo Crew: etiquetas de atributos não foram carregadas.", this);
        Transform old = cardDetails.transform.Find("EtiquetasDoCard");
        if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
        GameObject area = new GameObject("EtiquetasDoCard", typeof(RectTransform));
        badgeArea = (RectTransform)area.transform;
        badgeArea.SetParent(cardDetails.transform, false);
        badgeArea.SetAsFirstSibling();
        badgeArea.anchoredPosition = new Vector2(0f, -102f);
        badgeArea.sizeDelta = new Vector2(DragVisual.rect.width - 14f, 98f);

        ComboCrewWorkerView.CardBadge[] badges = owner.WorkerBadges(workerIndex);
        for (int i = 0; i < badges.Length && i < 2; i++)
        {
            int tone = badges[i].Tone;
            GameObject badge = new GameObject("Etiqueta" + i, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)badge.transform;
            rect.SetParent(badgeArea, false);
            rect.anchorMin = new Vector2(0f, .5f);
            rect.anchorMax = new Vector2(1f, .5f);
            rect.offsetMin = new Vector2(11f, 0f);
            rect.offsetMax = new Vector2(-11f, 0f);
            rect.anchoredPosition = new Vector2(0f, badges.Length == 1 ? 0f : i == 0 ? 22f : -22f);
            rect.sizeDelta = new Vector2(-22f, badges.Length == 1 ? 42f : 36f);
            Image background = badge.GetComponent<Image>();
            background.sprite = tone > 0 ? positiveBadge : tone < 0 ? negativeBadge : neutralBadge;
            background.type = background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = background.sprite != null ? Color.white
                : tone > 0 ? new Color32(119, 207, 155, 255)
                : tone < 0 ? new Color32(225, 91, 68, 255)
                : new Color32(246, 197, 55, 255);
            background.raycastTarget = false;

            GameObject caption = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform captionRect = (RectTransform)caption.transform;
            captionRect.SetParent(rect, false);
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(10f, 3f);
            captionRect.offsetMax = new Vector2(-10f, -3f);
            TextMeshProUGUI label = caption.GetComponent<TextMeshProUGUI>();
            label.font = traitLabel != null ? traitLabel.font : null;
            label.text = (tone > 0 ? "+ " : tone < 0 ? "- " : "") + badges[i].Label;
            label.fontSize = 16f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 15f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.fontStyle = FontStyles.Bold;
            label.fontWeight = FontWeight.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.outlineColor = Color.black;
            label.outlineWidth = .24f;
            label.raycastTarget = false;
        }
    }

    public void RefreshPortrait()
    {
        if (owner == null || cardDetails == null || portraitWhileDragging == null) return;
        Image body = cardDetails.transform.Find("CorpoMiniatura")?.GetComponent<Image>();
        Image face = cardDetails.transform.Find("RostoMiniatura")?.GetComponent<Image>();
        Image dragBody = portraitWhileDragging.GetComponent<Image>();
        Image dragFace = portraitWhileDragging.transform.Find("RostoNoArraste")?.GetComponent<Image>();
        Image hat = PortraitHat(cardDetails.transform, "ChapeuMiniatura", face);
        Image dragHat = PortraitHat(portraitWhileDragging.transform, "ChapeuNoArraste", dragFace);
        owner.CopyWorkerPortrait(workerIndex, body, face, hat);
        owner.CopyWorkerPortrait(workerIndex, dragBody, dragFace, dragHat);
    }

    private static Image PortraitHat(Transform parent, string name, Image face)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Image>();
        GameObject layer = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = layer.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        RectTransform faceRect = face != null ? face.rectTransform : null;
        rect.anchoredPosition = faceRect != null ? faceRect.anchoredPosition + new Vector2(0f, -4f) : Vector2.zero;
        rect.sizeDelta = faceRect != null ? new Vector2(faceRect.rect.width * 1.12f,
            faceRect.rect.height * .58f) : new Vector2(108f, 56f);
        Image image = layer.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    public void SetAssigned(bool value)
    {
        assigned = value;
        EnsureAssignedOverlay();
        assignedOverlay.SetActive(assigned && !IsDragging);
        Transform info = DragVisual.Find("IconeInformacao");
        if (info != null) info.SetAsLastSibling();
        if (DragCanvasGroup != null) DragCanvasGroup.alpha = 1f;
    }

    public void SetCardLayout(float width, float height)
    {
        if (DragVisual == null || cardDetails == null) return;
        boardSize = new Vector2(width, height);
        DragVisual.sizeDelta = boardSize;
        ApplyContentLayout(width, height, false);
    }

    private void ApplyContentLayout(float width, float height, bool compact)
    {
        RectTransform details = (RectTransform)cardDetails.transform;
        details.sizeDelta = new Vector2(width, height);
        float portraitSize = compact ? 88f : 112f;
        float portraitY = compact ? 24f : 50f;
        foreach (string part in new[] { "CorpoMiniatura", "RostoMiniatura" })
        {
            if (details.Find(part) is RectTransform portrait)
            {
                portrait.sizeDelta = new Vector2(portraitSize, portraitSize);
                portrait.anchoredPosition = new Vector2(0f, portraitY);
            }
        }
        if (details.Find("ChapeuMiniatura") is RectTransform hat)
        {
            hat.anchoredPosition = new Vector2(0f, portraitY - 4f);
            hat.sizeDelta = new Vector2(portraitSize * 1.12f, portraitSize * .58f);
        }
        if (details.Find("Funcoes") is RectTransform name)
        {
            name.anchoredPosition = new Vector2(0f, compact ? -43f : -29f);
            name.sizeDelta = new Vector2(width - 14f, compact ? 30f : 36f);
            if (name.GetComponent<TMP_Text>() is TMP_Text nameText)
                nameText.fontSizeMax = compact ? 18f : 21f;
        }
        if (badgeArea != null)
        {
            badgeArea.anchoredPosition = new Vector2(0f, compact ? -65f : -102f);
            badgeArea.sizeDelta = new Vector2(width - 14f, compact ? 56f : 98f);
            bool pair = badgeArea.childCount > 1;
            for (int i = 0; i < badgeArea.childCount; i++)
            {
                RectTransform badge = (RectTransform)badgeArea.GetChild(i);
                badge.gameObject.SetActive(!compact);
                float separation = compact ? 13f : 22f;
                badge.anchoredPosition = new Vector2(0f, pair ? i == 0 ? separation : -separation : 0f);
                badge.sizeDelta = new Vector2(-22f, compact ? pair ? 22f : 26f : pair ? 36f : 42f);
                if (badge.GetComponentInChildren<TMP_Text>() is TMP_Text text)
                    text.fontSizeMax = compact ? 11f : 15f;
            }
        }
        if (DragVisual.Find("FaixaCard") is RectTransform stripe)
            stripe.gameObject.SetActive(false);
    }

    private void EnsureAssignedOverlay()
    {
        if (assignedOverlay != null) return;
        Transform existing = DragVisual.Find("CardEmUso");
        if (existing != null) { assignedOverlay = existing.gameObject; return; }
        assignedOverlay = new GameObject("CardEmUso", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)assignedOverlay.transform;
        rect.SetParent(DragVisual, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image shade = assignedOverlay.GetComponent<Image>();
        shade.color = new Color(.035f, .04f, .05f, .72f);
        shade.raycastTarget = false;
        Image cardImage = DragVisual.GetComponent<Image>();
        if (cardImage != null)
        {
            shade.sprite = cardImage.sprite;
            shade.type = cardImage.type;
            shade.preserveAspect = cardImage.preserveAspect;
        }

        GameObject caption = new GameObject("LegendaEmUso", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform captionRect = (RectTransform)caption.transform;
        captionRect.SetParent(rect, false);
        captionRect.anchorMin = new Vector2(0f, .38f);
        captionRect.anchorMax = new Vector2(1f, .62f);
        captionRect.offsetMin = Vector2.zero;
        captionRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = caption.GetComponent<TextMeshProUGUI>();
        label.font = traitLabel != null ? traitLabel.font : null;
        label.text = "EM USO";
        label.fontSize = 21f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        assignedOverlay.SetActive(false);
    }

    private void Update()
    {
        if (IsDragging && owner != null)
            hoveredStation = owner.UpdateDropTarget(workerIndex, DragVisual);
        if (owner == null || !owner.CanNudgeWorkerCards || IsDragging || assigned)
        {
            idleTime = 0f;
            StopNudge();
            return;
        }

        idleTime += Time.unscaledDeltaTime;
        if (idleTime < idleNudgeDelay + workerIndex * 1.2f || !(transform is RectTransform rect)) return;
        idleTime = 0f;
        StopNudge();
        nudgeOriginRotation = rect.localRotation;
        float angle = nudgeOriginRotation.eulerAngles.z;
        idleNudge = DOTween.Sequence().SetTarget(this).SetUpdate(true)
            .Append(rect.DOLocalRotate(new Vector3(0f, 0f, angle + 5f), .28f).SetEase(Ease.InOutSine))
            .Append(rect.DOLocalRotate(new Vector3(0f, 0f, angle - 5f), .42f).SetEase(Ease.InOutSine))
            .Append(rect.DOLocalRotate(new Vector3(0f, 0f, angle), .3f).SetEase(Ease.OutSine));
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && !IsDragging
            && Time.unscaledTime - LastDragEndTime > .2f)
            owner?.OpenProfile(workerIndex);
    }
    protected override Vector3 RestPosition => owner != null && owner.CardHome(workerIndex) != null
        ? owner.CardHome(workerIndex).position : transform.position;

    protected override void DragStateChanged(bool dragging)
    {
        StopNudge();
        sizeTween?.Kill();
        if (DragVisual != null && boardSize != Vector2.zero)
        {
            ApplyContentLayout(dragging ? 176f : boardSize.x,
                dragging ? 200f : boardSize.y, dragging);
            sizeTween = DragVisual.DOSizeDelta(dragging ? new Vector2(176f, 200f) : boardSize,
                dragging ? .1f : .12f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
        if (DragCanvasGroup != null) DragCanvasGroup.alpha = 1f;
        if (assignedOverlay != null) assignedOverlay.SetActive(assigned && !dragging);
        Transform info = DragVisual != null ? DragVisual.Find("IconeInformacao") : null;
        if (info != null) info.gameObject.SetActive(!dragging);
        if (portraitWhileDragging != null) portraitWhileDragging.SetActive(false);
        if (cardDetails != null) cardDetails.SetActive(true);
        if (dragging)
        {
            owner.BeginMoveWorker(workerIndex);
            hoveredStation = -1;
            transform.SetAsLastSibling();
            clipboard.CloseForDrag(DragVisual);
        }
    }

    protected override void HighlightTargets(bool visible) => owner?.HighlightStations(visible, workerIndex);
    protected override void DragMoved()
    {
        clipboard?.TrackDraggedCard(DragVisual);
        hoveredStation = owner != null ? owner.UpdateDropTarget(workerIndex, DragVisual) : -1;
    }
    protected override void DragFinished(bool accepted)
    {
        if (!accepted && hoveredStation >= 0)
            accepted = owner != null && owner.AssignWorker(workerIndex, hoveredStation);
        hoveredStation = -1;
        if (!accepted) { AudioManager.Instance?.PlayError(); MequiHaptics.Reject(); }
        // O card volta à sua âncora local antes de a prancheta subir.
        // Assim não há tween para a posição fora da tela nem salto ao final.
        MoveHome(true);
        clipboard.OpenAfterDrag();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopNudge();
        sizeTween?.Kill();
        if (DragVisual != null && boardSize != Vector2.zero) DragVisual.sizeDelta = boardSize;
        DOTween.Kill(this);
    }

    private void StopNudge()
    {
        if (idleNudge == null) return;
        bool active = idleNudge.IsActive();
        idleNudge.Kill();
        if (active) transform.localRotation = nudgeOriginRotation;
        idleNudge = null;
    }
}
