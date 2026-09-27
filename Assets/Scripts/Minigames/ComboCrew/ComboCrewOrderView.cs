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
    [SerializeField, Min(.05f)] private float exitSeconds = .3f;
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
        for (int i = 0; i < itemIcons.Length; i++) itemIcons[i].SetActive((items & (1 << i)) != 0);
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
        card.localScale = Vector3.one * .94f;
        card.DOScale(1f, .2f).SetEase(Ease.OutBack).SetTarget(this);
        for (int i = 0; i < itemIcons.Length; i++)
            if (itemIcons[i].activeSelf)
                itemIcons[i].transform.DOPunchScale(Vector3.one * .12f, .2f, 1)
                    .SetDelay(.07f + i * .04f).SetTarget(this);
    }

    public void Refresh(float remainingFraction, RectTransform anchor)
    {
        float blend = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        shown = Mathf.Lerp(shown, Mathf.Clamp01(remainingFraction), blend);
        SetBar(shown);
        if (anchor != null && (card.position - anchor.position).sqrMagnitude > .5f
            && !DOTween.IsTweening(card))
            card.DOMove(anchor.position, .24f).SetEase(Ease.OutCubic).SetTarget(this);
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
            .Append(card.DOPunchScale(Vector3.one * .06f, .18f, 1));
        if (resultFlash != null) exit.Join(resultFlash.DOFade(.9f, .12f));
        exit
            .AppendInterval(.18f)
            .Append(canvasGroup.DOFade(0f, exitSeconds))
            .Append(card.DOScale(.85f, exitSeconds).SetEase(Ease.InCubic))
            .OnComplete(() => { exit = null; gameObject.SetActive(false); completed?.Invoke(); });
    }

    private void OnDisable() { exit?.Kill(); card?.DOKill(); DOTween.Kill(this); }
}
