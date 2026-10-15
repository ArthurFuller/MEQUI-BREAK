using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Card reutilizável; paciência diminui conforme o estado real do pedido.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewOrderView : MonoBehaviour
{
    [SerializeField] private RectTransform card;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image patienceFill;
    [SerializeField] private GameObject[] itemIcons;
    [SerializeField] private GameObject completedMark;
    [SerializeField] private GameObject failedMark;
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
    [SerializeField, Min(0f)] private float resolvePunchStrength = .06f;
    [SerializeField, Min(.01f)] private float resolvePunchSeconds = .18f;
    [SerializeField, Min(0f)] private float resultFlashAlpha = .9f;
    [SerializeField, Min(.01f)] private float resultFlashSeconds = .12f;
    [SerializeField, Min(0f)] private float resolveHoldSeconds = .18f;
    [SerializeField, Range(.1f, 1f)] private float exitScale = .85f;
    [SerializeField, Min(.05f)] private float exitSeconds = .3f;
    [SerializeField] private Ease exitEase = Ease.InCubic;
    private Sequence exit;
    private float shown = 1f;
    public bool Configured => card != null && canvasGroup != null && patienceFill != null
        && itemIcons != null && itemIcons.Length == 3 && itemIcons[0] != null
        && itemIcons[1] != null && itemIcons[2] != null
        && completedMark != null && failedMark != null;

    public void Show(int items, RectTransform anchor)
    {
        exit?.Kill();
        card.DOKill();
        card.position = anchor.position;
        card.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
        completedMark.SetActive(false);
        failedMark.SetActive(false);
        ArrangeItems(items);
        if (resultFlash != null)
        {
            Color color = resultFlash.color;
            color.a = 0f;
            resultFlash.color = color;
            resultFlash.gameObject.SetActive(false);
        }
        shown = 1f;
        patienceFill.type = Image.Type.Simple;
        SetBar(1f);
        card.localScale = Vector3.one * showStartScale;
        card.DOScale(1f, showSeconds).SetEase(showEase).SetTarget(this);
        for (int i = 0; i < itemIcons.Length; i++)
            if (itemIcons[i].activeSelf)
                itemIcons[i].transform.DOPunchScale(Vector3.one * itemPunchStrength, itemPunchSeconds, 1)
                    .SetDelay(itemPunchStartDelay + i * itemPunchStagger).SetTarget(this);
    }

    private void ArrangeItems(int items)
    {
        int visibleCount = 0;
        float totalWidth = 0f;
        for (int i = 0; i < itemIcons.Length; i++)
        {
            bool visible = (items & (1 << i)) != 0;
            itemIcons[i].SetActive(visible);
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
        RectTransform bar = patienceFill.rectTransform;
        Vector3 scale = bar.localScale;
        scale.x = Mathf.Clamp01(value);
        bar.localScale = scale;
        Color red = new Color(.9f, .33f, .28f);
        Color yellow = new Color(.94f, .73f, .26f);
        Color green = new Color(.43f, .73f, .53f);
        patienceFill.color = value > .5f
            ? Color.Lerp(yellow, green, (value - .5f) * 2f)
            : Color.Lerp(red, yellow, value * 2f);
    }

    public void Resolve(bool delivered, Action completed)
    {
        if (delivered) completedMark.SetActive(true);
        else failedMark.SetActive(true);
        exit?.Kill();
        card.DOKill();
        if (resultFlash != null)
        {
            resultFlash.gameObject.SetActive(true);
            resultFlash.color = delivered
                ? new Color(.38f, .86f, .55f, 0f) : new Color(1f, .35f, .28f, 0f);
        }
        exit = DOTween.Sequence().SetTarget(this)
            .Append(card.DOPunchScale(Vector3.one * resolvePunchStrength, resolvePunchSeconds, 1));
        if (resultFlash != null) exit.Join(resultFlash.DOFade(resultFlashAlpha, resultFlashSeconds));
        exit
            .AppendInterval(resolveHoldSeconds)
            .Append(canvasGroup.DOFade(0f, exitSeconds))
            .Append(card.DOScale(exitScale, exitSeconds).SetEase(exitEase))
            .OnComplete(() => { exit = null; gameObject.SetActive(false); completed?.Invoke(); });
    }

    private void OnDisable() { exit?.Kill(); card?.DOKill(); DOTween.Kill(this); }
}
