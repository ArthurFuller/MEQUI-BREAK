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
        for (int i = 0; i < itemIcons.Length; i++) itemIcons[i].SetActive(i < items);
        shown = 1f;
        patienceFill.type = Image.Type.Simple;
        SetBar(1f);
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
    }

    public void Resolve(bool delivered, Action completed)
    {
        if (delivered) completedMark.SetActive(true);
        else failedMark.SetActive(true);
        exit?.Kill();
        exit = DOTween.Sequence().SetTarget(this)
            .Append(card.DOPunchScale(Vector3.one * .08f, .18f, 1))
            .AppendInterval(.18f)
            .Join(canvasGroup.DOFade(0f, exitSeconds))
            .Append(card.DOScale(.85f, exitSeconds).SetEase(Ease.InCubic))
            .OnComplete(() => { exit = null; gameObject.SetActive(false); completed?.Invoke(); });
    }

    private void OnDisable() { exit?.Kill(); card?.DOKill(); }
}
