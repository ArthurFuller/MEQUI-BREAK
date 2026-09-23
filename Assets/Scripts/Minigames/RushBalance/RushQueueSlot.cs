using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RushQueueSlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private RushBalanceController controller;
    [SerializeField, Min(0)] private int rank;
    [SerializeField] private GameObject highlight;
    [SerializeField] private RectTransform feedbackTarget;
    [SerializeField] private CanvasGroup highlightGroup;
    [SerializeField, Range(1f, 1.12f)] private float highlightedScale = 1.045f;
    [SerializeField, Min(.01f)] private float transitionSeconds = .12f;
    [SerializeField, Min(0f)] private float dropPunch = .04f;

    private Tween feedbackTween;
    private Vector3 baseScale;
    private bool showing;

    public bool ConfiguredFor(RushBalanceController owner, int index) =>
        controller == owner && rank == index && highlight != null
        && feedbackTarget != null && highlightGroup != null;

    private void Awake()
    {
        if (feedbackTarget != null) baseScale = feedbackTarget.localScale;
        if (highlightGroup != null) highlightGroup.alpha = 0f;
        if (highlight != null) highlight.SetActive(false);
    }

    public void Highlight(bool value)
    {
        if (highlight == null || highlightGroup == null || feedbackTarget == null || showing == value) return;
        showing = value;
        feedbackTween?.Kill();
        if (value) highlight.SetActive(true);

        feedbackTween = DOTween.Sequence().SetTarget(this)
            .Join(highlightGroup.DOFade(value ? 1f : 0f, transitionSeconds).SetEase(Ease.OutQuad))
            .Join(feedbackTarget.DOScale(value ? baseScale * highlightedScale : baseScale, transitionSeconds)
                .SetEase(value ? Ease.OutBack : Ease.OutQuad))
            .OnComplete(() =>
            {
                feedbackTween = null;
                if (!showing) highlight.SetActive(false);
            });
    }

    public void OnDrop(PointerEventData data)
    {
        if (controller == null || data.pointerDrag == null) return;
        RushOrderView order = data.pointerDrag.GetComponent<RushOrderView>();
        if (order != null && order.Owner == controller && order.OwnsPointer(data))
        {
            order.AcceptCurrentDrop();
            controller.Reorder(order.OrderId, rank);
            if (dropPunch > 0f)
                feedbackTarget.DOPunchScale(Vector3.one * dropPunch, transitionSeconds * 1.8f, 2, .35f)
                    .SetTarget(this);
        }
    }

    private void OnDisable()
    {
        feedbackTween?.Kill();
        DOTween.Kill(this);
        showing = false;
        if (feedbackTarget != null) feedbackTarget.localScale = baseScale;
        if (highlightGroup != null) highlightGroup.alpha = 0f;
        if (highlight != null) highlight.SetActive(false);
    }
}
