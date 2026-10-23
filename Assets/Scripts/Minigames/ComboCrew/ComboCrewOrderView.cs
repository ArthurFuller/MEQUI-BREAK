using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Card reutilizável; paciência diminui conforme o estado real do pedido.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewOrderView : MonoBehaviour
{
    [SerializeField] private RectTransform card;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image patienceFill;
    [SerializeField, Range(0f, 6f), Tooltip("Recuo do preenchimento sob o contorno preto da barra.")]
    private float patienceBarInset = 1f;
    [SerializeField] private GameObject[] itemIcons;
    [SerializeField] private Image resultFlash;
    [Header("Animação do card")]
    [SerializeField, Range(.5f, 1f)] private float showStartScale = .94f;
    [SerializeField, Min(.01f)] private float showSeconds = .2f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField, Min(0f)] private float itemPunchStrength = .12f;
    [SerializeField, Min(.01f)] private float itemPunchSeconds = .2f;
    [SerializeField, Min(0f)] private float itemPunchStartDelay = .07f;
    [SerializeField, Min(0f)] private float itemPunchStagger = .04f;
    [SerializeField, Min(1f)] private float patienceSmoothing = 10f;
    [SerializeField, Min(.01f)] private float queueMoveSeconds = .24f;
    [SerializeField] private Ease queueMoveEase = Ease.OutCubic;
    [Header("Feedback ao concluir pedido")]
    [SerializeField, Min(0f)] private float resultFlashAlpha = .28f;
    [SerializeField, Min(.01f)] private float resultFlashSeconds = .2f;
    [SerializeField, Min(0f)] private float resolveHoldSeconds = .24f;
    [SerializeField, Range(.1f, 1f)] private float exitScale = .85f;
    [SerializeField, Min(.05f)] private float exitSeconds = .3f;
    [SerializeField] private Ease exitEase = Ease.InCubic;
    private Sequence exit;
    private float shown = 1f;
    public bool Configured => card != null && canvasGroup != null && patienceFill != null
        && itemIcons != null && itemIcons.Length == 3 && itemIcons[0] != null
        && itemIcons[1] != null && itemIcons[2] != null
        && resultFlash != null;

    public void Show(int items, RectTransform anchor, Sprite[] sprites = null)
    {
        exit?.Kill();
        card.DOKill();
        card.position = anchor.position;
        card.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
        ArrangeItems(items, sprites);
        if (resultFlash != null)
        {
            PrepareResultFlash();
            Color color = resultFlash.color;
            color.a = 0f;
            resultFlash.color = color;
            resultFlash.gameObject.SetActive(false);
        }
        shown = 1f;
        patienceFill.type = Image.Type.Sliced;
        ComboCrewProgressBorder.Ensure(patienceFill);
        SetBar(1f);
        card.localScale = Vector3.one * showStartScale;
        card.DOScale(1f, showSeconds).SetEase(showEase).SetTarget(this);
        for (int i = 0; i < itemIcons.Length; i++)
            if (itemIcons[i].activeSelf)
                itemIcons[i].transform.DOPunchScale(Vector3.one * itemPunchStrength, itemPunchSeconds, 1)
                    .SetDelay(itemPunchStartDelay + i * itemPunchStagger).SetTarget(this);
    }

    private void PrepareResultFlash()
    {
        Image background = card != null ? card.GetComponent<Image>() : null;
        if (background == null || background.sprite == null) return;
        resultFlash.sprite = background.sprite;
        resultFlash.type = background.type;
        resultFlash.preserveAspect = background.preserveAspect;
        resultFlash.material = null;
        resultFlash.fillCenter = true;
    }

    private void ArrangeItems(int items, Sprite[] sprites)
    {
        int visibleCount = 0;
        float totalWidth = 0f;
        for (int i = 0; i < itemIcons.Length; i++)
        {
            bool visible = (items & (1 << i)) != 0;
            itemIcons[i].SetActive(visible);
            if (visible && sprites != null && i < sprites.Length)
                ComboCrewItemVisual.Set(itemIcons[i], sprites[i]);
            if (!visible || !(itemIcons[i].transform is RectTransform rect)) continue;
            totalWidth += rect.rect.width;
            visibleCount++;
        }

        const float gap = 2f;
        totalWidth += Mathf.Max(0, visibleCount - 1) * gap;
        float left = -totalWidth * .5f;
        for (int i = 0; i < itemIcons.Length; i++)
        {
            if (!itemIcons[i].activeSelf || !(itemIcons[i].transform is RectTransform rect)) continue;
            float width = rect.rect.width;
            rect.anchoredPosition = new Vector2(left + width * .5f, rect.anchoredPosition.y);
            left += width + gap;
        }
    }

    public void Refresh(float remainingFraction, RectTransform anchor)
    {
        float blend = 1f - Mathf.Exp(-patienceSmoothing * Time.unscaledDeltaTime);
        shown = Mathf.Lerp(shown, Mathf.Clamp01(remainingFraction), blend);
        SetBar(shown);
        if (anchor != null && (card.position - anchor.position).sqrMagnitude > .5f
            && !DOTween.IsTweening(card))
            card.DOMove(anchor.position, queueMoveSeconds).SetEase(queueMoveEase).SetTarget(this);
    }

    private void SetBar(float value)
    {
        RectTransform fillRect = patienceFill.rectTransform;
        RectTransform track = (RectTransform)fillRect.parent;
        float inset = Mathf.Clamp(patienceBarInset, 0f, track.rect.height * .5f);
        float availableWidth = Mathf.Max(0f, track.rect.width - inset * 2f);
        float width = availableWidth * Mathf.Clamp01(value);
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(.5f, .5f);
        fillRect.pivot = new Vector2(0f, .5f);
        fillRect.anchoredPosition = new Vector2(-track.rect.width * .5f + inset, 0f);
        fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(0f, track.rect.height - inset * 2f));
        fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        patienceFill.enabled = width > .5f;
        Color red = new Color(.9f, .33f, .28f);
        Color yellow = new Color(.94f, .73f, .26f);
        Color green = new Color(.43f, .73f, .53f);
        patienceFill.color = value > .5f
            ? Color.Lerp(yellow, green, (value - .5f) * 2f)
            : Color.Lerp(red, yellow, value * 2f);
    }

    public void Resolve(bool delivered, Action completed)
    {
        exit?.Kill();
        card.DOKill();
        if (resultFlash != null)
        {
            resultFlash.gameObject.SetActive(true);
            resultFlash.color = delivered
                ? new Color(.38f, .86f, .55f, 0f) : new Color(1f, .35f, .28f, 0f);
        }
        PrepareResultFlash();
        exit = DOTween.Sequence().SetTarget(this);
        exit.Append(resultFlash.DOFade(resultFlashAlpha, resultFlashSeconds));
        exit
            .AppendInterval(resolveHoldSeconds)
            .Append(canvasGroup.DOFade(0f, exitSeconds))
            .Join(card.DOScale(exitScale, exitSeconds).SetEase(exitEase))
            .OnComplete(() => { exit = null; gameObject.SetActive(false); completed?.Invoke(); });
    }

    private void OnDisable() { exit?.Kill(); card?.DOKill(); DOTween.Kill(this); }
}
