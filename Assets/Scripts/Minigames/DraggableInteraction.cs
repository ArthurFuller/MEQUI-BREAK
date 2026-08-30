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

    [Header("Animação de reaparecimento")]
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
    private Coroutine returnRoutine;
    private Sequence respawnSequence;

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
        StopReturnRoutine(completeReturn: false);
        accepted = false;
        originalParent = transform.parent;
        originalWorldPosition = rectTransform.position;
        canvasGroup.blocksRaycasts = false;

        // Move o item para o Canvas raiz para renderizá-lo acima dos demais.
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
            returnRoutine = StartCoroutine(ReturnToOrigin());
    }

    public void AcceptDrop()
    {
        StopReturnRoutine(completeReturn: false);
        accepted = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Reutiliza o item no centro do contêiner e reproduz sua animação sem criar clone.
    /// </summary>
    public void Respawn()
    {
        StopReturnRoutine(completeReturn: false);
        Transform container = originalParent != null ? originalParent : transform.parent;
        if (container == null)
            return;

        transform.SetParent(container, false);
        rectTransform.anchoredPosition = Vector2.zero;
        canvasGroup.blocksRaycasts = true;
        AnimateRespawn();
    }

    private void AnimateRespawn()
    {
        if (respawnDuration <= 0f)
            return;

        if (respawnSequence != null && respawnSequence.IsActive())
            respawnSequence.Kill();

        // Estado inicial.
        rectTransform.localScale = Vector3.one * respawnScaleFrom;
        canvasGroup.alpha = 0f;

        respawnSequence = DOTween.Sequence().SetTarget(this);

        // Primeira fase: cresce até a escala excedente.
        respawnSequence.Join(rectTransform.DOScale(respawnScaleTo, respawnDuration * 0.6f)
            .SetEase(Ease.OutBack));

        // Segunda fase: retorna à escala final.
        respawnSequence.Append(rectTransform.DOScale(respawnScaleFinal, respawnDuration * 0.4f)
            .SetEase(Ease.OutCubic));

        // A transparência retorna de 0 para 1.
        respawnSequence.Join(canvasGroup.DOFade(1f, respawnDuration)
            .SetEase(Ease.OutQuad));

        respawnSequence.OnComplete(CompleteRespawnAnimation);
    }

    private void CompleteRespawnAnimation() => respawnSequence = null;

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

        returnRoutine = null;
    }

    private void StopReturnRoutine(bool completeReturn)
    {
        bool wasReturning = returnRoutine != null;
        if (!wasReturning && !completeReturn)
            return;

        if (wasReturning)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        if (originalParent != null && transform.parent != originalParent)
            transform.SetParent(originalParent, true);

        if (completeReturn && originalParent != null)
            rectTransform.position = originalWorldPosition;
    }

    private void OnDisable()
    {
        StopReturnRoutine(completeReturn: true);

        if (respawnSequence != null && respawnSequence.IsActive())
            respawnSequence.Kill();

        respawnSequence = null;
        rectTransform.localScale = Vector3.one * respawnScaleFinal;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }
}
