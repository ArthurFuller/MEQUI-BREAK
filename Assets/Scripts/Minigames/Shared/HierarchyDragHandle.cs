using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Move apenas o objeto já montado na Hierarchy, sem trocar seu pai.</summary>
public abstract class HierarchyDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Arraste — referências da cena")]
    [SerializeField] private RectTransform visual;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField, Min(.01f)] private float returnSeconds = .16f;
    private Vector3 pointerOffset;
    private Tween movement;
    private Tween scaleFeedback;
    private Vector3 originalScale;
    private bool scaleCached;
    private int pointerId;
    private bool dropAccepted;
    public bool IsDragging { get; private set; }
    public float LastDragEndTime { get; private set; } = -10f;
    protected CanvasGroup DragCanvasGroup => canvasGroup;
    public bool OwnsPointer(PointerEventData data) => IsDragging && data.pointerId == pointerId;
    public string DragConfigurationError => visual == null ? "visual"
        : canvasGroup == null ? "canvasGroup" : canvasRect == null ? "canvasRect" : null;
    public bool DragConfigured => DragConfigurationError == null;
    protected abstract bool CanDrag { get; }
    protected abstract Vector3 RestPosition { get; }
    protected virtual void HighlightTargets(bool visible) { }
    protected virtual void DragStateChanged(bool dragging) { }
    protected virtual void DragFinished(bool accepted) { }

    protected void MarkDropAccepted()
    {
        if (IsDragging) dropAccepted = true;
    }

    protected virtual void Awake()
    {
        CacheScale();
    }

    private void CacheScale()
    {
        if (visual == null || scaleCached) return;
        originalScale = visual.localScale;
        scaleCached = true;
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (IsDragging || data.button != PointerEventData.InputButton.Left
            || !DragConfigured || !CanDrag || !Point(data, out Vector3 point)) return;
        CacheScale();
        movement?.Kill();
        scaleFeedback?.Kill();
        scaleFeedback = visual.DOScale(originalScale * 1.04f, .08f).SetEase(Ease.OutQuad);
        pointerOffset = visual.position - point;
        IsDragging = true;
        dropAccepted = false;
        pointerId = data.pointerId;
        DragStateChanged(true);
        canvasGroup.blocksRaycasts = false;
        HighlightTargets(true);
        AudioManager.Instance?.PlayClick();
        MequiHaptics.Selection();
    }

    public void OnDrag(PointerEventData data)
    {
        if (!OwnsPointer(data)) return;
        if (!CanDrag) { CancelDrag(false); return; }
        if (Point(data, out Vector3 point)) visual.position = point + pointerOffset;
    }

    public void OnEndDrag(PointerEventData data)
    {
        if (!OwnsPointer(data)) return;
        LastDragEndTime = Time.unscaledTime;
        bool accepted = dropAccepted;
        CancelDrag(false);
        DragFinished(accepted);
    }

    public void CancelDrag(bool instant)
    {
        CacheScale();
        IsDragging = false;
        dropAccepted = false;
        DragStateChanged(false);
        HighlightTargets(false);
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        if (visual != null)
        {
            scaleFeedback?.Kill();
            if (instant) visual.localScale = originalScale;
            else scaleFeedback = visual.DOScale(originalScale, .12f).SetEase(Ease.OutQuad);
        }
        MoveHome(instant);
    }

    public void MoveHome(bool instant = false)
    {
        if (!DragConfigured || IsDragging) return;
        Vector3 target = RestPosition;
        if (!instant && (visual.position - target).sqrMagnitude < .01f) return;
        movement?.Kill();
        if (instant) visual.position = target;
        else movement = visual.DOMove(target, returnSeconds).SetEase(Ease.OutCubic);
    }

    private bool Point(PointerEventData data, out Vector3 position)
    {
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect, data.position, data.pressEventCamera, out position);
    }

    protected virtual void OnDisable()
    {
        CacheScale();
        IsDragging = false;
        DragStateChanged(false);
        HighlightTargets(false);
        movement?.Kill();
        scaleFeedback?.Kill();
        if (visual != null) visual.localScale = originalScale;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }
}
