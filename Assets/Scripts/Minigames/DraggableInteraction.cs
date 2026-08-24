using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class DraggableInteraction : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private string interactionId = "interaction_01";
    [SerializeField] private string reactionTrigger = "Happy";
    [SerializeField, TextArea] private string feedbackMessage = "";
    [SerializeField, Min(0f)] private float returnDuration = 0.15f;

    [Header("Respawn Animation")]
    [SerializeField, Min(0.05f)] private float respawnDuration = 0.35f;
    [SerializeField, Min(0f)] private float respawnScaleFrom = 0.8f;
    [SerializeField, Min(0f)] private float respawnScaleTo = 1.05f;
    [SerializeField, Min(0f)] private float respawnScaleFinal = 1f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private Vector3 originalWorldPosition;
    private Canvas rootCanvas;
    private bool accepted;

    public string InteractionId => interactionId;
    public string ReactionTrigger => reactionTrigger;
    public string FeedbackMessage => feedbackMessage;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        accepted = false;
        originalParent = transform.parent;
        originalWorldPosition = rectTransform.position;
        canvasGroup.blocksRaycasts = false;

        // Re-attach to the root canvas so the item renders above everything
        // (siblings, container children) without moving the container itself.
        if (rootCanvas != null && transform.parent != rootCanvas.transform)
            transform.SetParent(rootCanvas.transform, true);

        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rootCanvas == null)
            return;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : eventData.pressEventCamera;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                eventData.position,
                eventCamera,
                out Vector3 worldPoint))
        {
            rectTransform.position = worldPoint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        if (!accepted)
            StartCoroutine(ReturnToOrigin());
    }

    public void AcceptDrop()
    {
        accepted = true;
        canvasGroup.blocksRaycasts = true;
        // NOTE: Do NOT call ReturnToOrigin here - ConsumeAndRespawn handles
        // the old item destruction and new clone animation.
        // This method only marks the drop as accepted so OnEndDrag doesn't
        // trigger a return animation.
    }

    /// <summary>
    /// Destroys this draggable and spawns a fresh identical copy inside
    /// the given container. The container is derived from the original
    /// parent captured at OnBeginDrag when available.
    /// Returns the new clone so the caller can update its template reference.
    /// </summary>
    public GameObject ConsumeAndRespawn(GameObject itemTemplate)
    {
        Transform container = originalParent != null ? originalParent : transform.parent;
        GameObject clone = null;

        if (itemTemplate != null && container != null)
        {
            // worldPositionStays:false forces the clone to inherit the template's
            // local RectTransform (anchoredPosition 0,0) inside the new parent,
            // so the item respawns at the container's center, not where it
            // happened to be dropped.
            clone = Instantiate(itemTemplate, container, false);
            RectTransform cloneRect = clone.transform as RectTransform;
            if (cloneRect != null)
                cloneRect.anchoredPosition = Vector2.zero;

            // CRITICAL FIX: Ensure the new clone has blocksRaycasts = true
            // The template might have blocksRaycasts = false from dragging state
            CanvasGroup cloneCanvasGroup = clone.GetComponent<CanvasGroup>();
            if (cloneCanvasGroup != null)
                cloneCanvasGroup.blocksRaycasts = true;

            // Animate respawn: Scale + Fade (following ResultPopup pattern)
            AnimateRespawn(cloneRect, cloneCanvasGroup);
        }

        Destroy(gameObject);
        return clone;
    }

    private void AnimateRespawn(RectTransform cloneRect, CanvasGroup cloneCanvasGroup)
    {
        if (cloneRect == null || respawnDuration <= 0f)
            return;

        // Start state
        cloneRect.localScale = Vector3.one * respawnScaleFrom;
        if (cloneCanvasGroup != null)
            cloneCanvasGroup.alpha = 0f;

        // Build sequence following ResultPopup pattern (OutQuad/OutCubic for smooth feel)
        // Sequence lives on the CLONE, not on the old item being destroyed
        var sequence = DOTween.Sequence();

        // Scale: 0.8 → 1.05 (overshoot) → 1.0
        // Phase 1: 0.8 → 1.05 (overshoot for "pop" feel)
        sequence.Join(cloneRect.DOScale(respawnScaleTo, respawnDuration * 0.6f)
            .SetEase(Ease.OutBack));

        // Phase 2: 1.05 → 1.0 (settle)
        sequence.Append(cloneRect.DOScale(respawnScaleFinal, respawnDuration * 0.4f)
            .SetEase(Ease.OutCubic));

        // Fade in: 0 → 1
        if (cloneCanvasGroup != null)
        {
            sequence.Join(cloneCanvasGroup.DOFade(1f, respawnDuration)
                .SetEase(Ease.OutQuad));
        }

        // No need to track sequence - it's on the clone which persists
        // OnComplete is optional since the clone stays alive
    }

    private IEnumerator ReturnToOrigin()
    {
        Vector3 start = rectTransform.position;
        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = returnDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / returnDuration);
            rectTransform.position = Vector3.Lerp(start, originalWorldPosition, t);
            yield return null;
        }

        rectTransform.position = originalWorldPosition;

        if (originalParent != null)
            transform.SetParent(originalParent, true);
    }
}
