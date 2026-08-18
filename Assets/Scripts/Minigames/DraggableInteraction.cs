using System.Collections;
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
        StartCoroutine(ReturnToOrigin());
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
