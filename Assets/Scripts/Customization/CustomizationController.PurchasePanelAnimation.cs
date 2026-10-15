using DG.Tweening;
using UnityEngine;

public sealed partial class CustomizationController
{
    /// <summary>
    /// Anima a entrada do painel de compra com escala e transparência.
    /// </summary>
    private void AnimatePurchasePanelEnter() => AnimatePurchasePanel(true);

    /// <summary>
    /// Anima a saída do painel e invoca o callback ao concluir.
    /// </summary>
    private void AnimatePurchasePanelExit(System.Action onComplete = null) =>
        AnimatePurchasePanel(false, onComplete);

    private void AnimatePurchasePanel(bool entering, System.Action onComplete = null)
    {
        if (purchaseConfirmPanel == null)
        {
            if (!entering)
                onComplete?.Invoke();
            return;
        }

        if (entering)
            purchaseConfirmPanel.SetActive(true);

        if (!EnsurePurchasePanelReferences())
        {
            if (!entering)
            {
                purchaseConfirmPanel.SetActive(false);
                onComplete?.Invoke();
            }
            return;
        }

        KillPurchasePanelTween();
        _purchasePanelCanvasGroup.DOKill();
        _purchasePanelRect.DOKill();

        float duration = entering ? purchasePanelEnterDuration : purchasePanelExitDuration;
        Ease fadeEase = entering ? Ease.OutQuad : Ease.InQuad;
        Ease scaleEase = entering ? Ease.OutBack : Ease.InQuad;
        float targetAlpha = entering ? 1f : 0f;
        Vector3 targetScale = entering ? Vector3.one : Vector3.one * 0.95f;

        if (entering)
        {
            _purchasePanelCanvasGroup.alpha = 0f;
            _purchasePanelRect.localScale = Vector3.one * purchasePanelEnterScale;
        }

        _purchasePanelSequence = DOTween.Sequence().SetUpdate(UpdateType.Late);
        _purchasePanelSequence.Join(
            _purchasePanelCanvasGroup.DOFade(targetAlpha, duration)
                .SetEase(fadeEase)
                .SetUpdate(UpdateType.Late));
        _purchasePanelSequence.Join(
            _purchasePanelRect.DOScale(targetScale, duration)
                .SetEase(scaleEase)
                .SetUpdate(UpdateType.Late));

        _purchasePanelSequence.OnComplete(() =>
        {
            _purchasePanelSequence = null;
            if (!entering)
            {
                purchaseConfirmPanel.SetActive(false);
                _purchasePanelRect.localScale = Vector3.one;
            }
            onComplete?.Invoke();
        });
        _purchasePanelSequence.Play();
    }

    private void KillPurchasePanelTween()
    {
        if (_purchasePanelSequence != null && _purchasePanelSequence.IsActive())
            _purchasePanelSequence.Kill(false);

        _purchasePanelSequence = null;
    }

    private bool EnsurePurchasePanelReferences()
    {
        if (purchaseConfirmPanel == null)
            return false;

        _purchasePanelRect ??= purchaseConfirmPanel.transform as RectTransform;
        _purchasePanelCanvasGroup ??= purchasePanelCanvasGroup;

        return _purchasePanelRect != null && _purchasePanelCanvasGroup != null;
    }

}
