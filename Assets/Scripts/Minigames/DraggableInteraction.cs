using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class DraggableInteraction : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Dados da interação")]
    [SerializeField] private string interactionId = "interaction_01";
    [SerializeField] private string reactionTrigger = "Happy";
    [SerializeField, TextArea] private string feedbackMessage = "";

    [Header("Retorno ao slot")]
    [SerializeField, Min(0f)] private float returnDuration = 0.15f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originParent;
    private Vector3 originLocalPosition;
    private Quaternion originLocalRotation;
    private Vector3 originLocalScale;
    private Canvas interactionCanvas;
    private bool hasOrigin;
    private bool accepted;
    private Coroutine returnRoutine;

    public string InteractionId => interactionId;
    public string ReactionTrigger => reactionTrigger;
    public string FeedbackMessage => feedbackMessage;

    public event System.Action<DraggableInteraction> DragStarted;
    public event System.Action<DraggableInteraction> ReturnedToOrigin;

    private void Awake()
    {
        CacheComponents();
        CacheOrigin();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        StopReturnRoutine(completeReturn: false);
        accepted = false;

        // Atualiza o slot somente quando o card já está sob o pai de origem.
        // Durante um retorno interrompido, o pai ainda é o Canvas interativo.
        if (!hasOrigin || transform.parent == originParent)
            CacheOrigin();

        canvasGroup.blocksRaycasts = false;

        // Move o card para o Canvas da área interativa, acima dos demais cards,
        // sem cair para baixo do overlay do tutorial.
        if (interactionCanvas != null && transform.parent != interactionCanvas.transform)
            transform.SetParent(interactionCanvas.transform, true);

        transform.SetAsLastSibling();
        DragStarted?.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (interactionCanvas == null)
            return;

        RectTransform canvasRect = interactionCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Camera eventCamera = interactionCanvas.renderMode == RenderMode.ScreenSpaceOverlay
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

    /// <summary>
    /// Marca o card como aceito e o desativa, mantendo o slot de origem vazio.
    /// </summary>
    public void AcceptDrop()
    {
        StopReturnRoutine(completeReturn: false);
        accepted = true;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Reativa o card e o devolve imediatamente ao slot original sem criar clones.
    /// </summary>
    public void ResetToOrigin()
    {
        // Os cards começam inativos na cena. Nesse estado, Awake ainda não foi
        // executado quando o controlador solicita o primeiro reset.
        CacheComponents();
        StopReturnRoutine(completeReturn: true);
        accepted = false;

        if (!hasOrigin)
            CacheOrigin();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (originParent != null)
            transform.SetParent(originParent, false);

        ApplyOriginTransform();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Mantém compatibilidade com eventuais referências antigas do Inspector.
    /// </summary>
    public void Respawn() => ResetToOrigin();

    private void CacheOrigin()
    {
        CacheComponents();
        if (rectTransform == null)
            return;

        originParent = transform.parent;
        originLocalPosition = rectTransform.localPosition;
        originLocalRotation = rectTransform.localRotation;
        originLocalScale = rectTransform.localScale;
        hasOrigin = originParent != null;
    }

    private void CacheComponents()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // Usa o Canvas interativo mais próximo. Na Energy Station ele fica
        // serializado acima do overlay do tutorial, preservando drag e drop.
        if (interactionCanvas == null)
            interactionCanvas = GetComponentInParent<Canvas>(true);
    }

    private IEnumerator ReturnToOrigin()
    {
        Vector3 start = rectTransform.position;
        Vector3 target = originParent != null
            ? originParent.TransformPoint(originLocalPosition)
            : start;
        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = returnDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / returnDuration);

            rectTransform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        if (originParent != null)
        {
            transform.SetParent(originParent, false);
            ApplyOriginTransform();
        }
        else
        {
            rectTransform.position = target;
        }

        returnRoutine = null;
        ReturnedToOrigin?.Invoke(this);
    }

    private void StopReturnRoutine(bool completeReturn)
    {
        bool wasReturning = returnRoutine != null;
        if (wasReturning)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        if (!completeReturn || originParent == null)
            return;

        transform.SetParent(originParent, false);
        ApplyOriginTransform();
    }

    private void ApplyOriginTransform()
    {
        rectTransform.localPosition = originLocalPosition;
        rectTransform.localRotation = originLocalRotation;
        rectTransform.localScale = originLocalScale;
    }

    private void OnDisable()
    {
        // Durante OnDisable o Unity ainda está processando a desativação;
        // alterar a hierarquia nesse ponto gera erro de ativação/desativação.
        // O reparenting é feito com segurança no reset ou no fim do retorno.
        StopReturnRoutine(completeReturn: false);
        rectTransform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }
}
