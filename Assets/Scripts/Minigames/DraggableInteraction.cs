using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    [Header("Estado visual bloqueado")]
    [Tooltip("Arte do card. Se não for atribuída, o filho cujo nome começa com 'Card' será usado.")]
    [SerializeField] private GameObject cardVisual;
    [Tooltip("Contorno tracejado do slot. Se não for atribuído, o filho cujo nome começa com 'Slot' será usado.")]
    [SerializeField] private GameObject slotVisual;
    [SerializeField] private Color lockedTint = new Color(0.28f, 0.28f, 0.28f, 1f);

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform originParent;
    private Vector3 originLocalPosition;
    private Quaternion originLocalRotation;
    private Vector3 originLocalScale;
    private Canvas interactionCanvas;
    private bool hasOrigin;
    private bool accepted;
    private bool sessionLocked;
    private bool dragInProgress;
    private Coroutine returnRoutine;
    private Graphic[] cardGraphics;
    private Color[] originalGraphicColors;

    public string InteractionId => interactionId;
    public string ReactionTrigger => reactionTrigger;
    public string FeedbackMessage => feedbackMessage;
    public bool IsAvailable => !accepted && !sessionLocked && gameObject.activeInHierarchy;

    public event System.Action<DraggableInteraction> DragStarted;
    public event System.Action<DraggableInteraction> ReturnedToOrigin;

    private void Awake()
    {
        CacheComponents();
        CacheOrigin();
        CacheVisualReferences();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (accepted || sessionLocked)
            return;

        StopReturnRoutine(completeReturn: false);
        dragInProgress = true;

        // Atualiza o slot somente quando o card já está sob o pai de origem.
        // Durante um retorno interrompido, o pai ainda é o Canvas interativo.
        if (!hasOrigin || transform.parent == originParent)
            CacheOrigin();

        canvasGroup.blocksRaycasts = false;
        AudioManager.Instance?.PlayEnergyDrag();

        // Move o card para o Canvas da área interativa, acima dos demais cards,
        // sem cair para baixo do overlay do tutorial.
        if (interactionCanvas != null && transform.parent != interactionCanvas.transform)
            transform.SetParent(interactionCanvas.transform, true);

        transform.SetAsLastSibling();
        DragStarted?.Invoke(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragInProgress || interactionCanvas == null)
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
        if (!dragInProgress)
            return;

        dragInProgress = false;
        canvasGroup.blocksRaycasts = true;

        if (!accepted)
            returnRoutine = StartCoroutine(ReturnToOrigin());
    }

    /// <summary>
    /// Marca o card como aceito e esconde sua arte, mantendo somente o slot vazio.
    /// </summary>
    public void AcceptDrop()
    {
        StopReturnRoutine(completeReturn: false);
        accepted = true;
        dragInProgress = false;

        if (originParent != null)
            transform.SetParent(originParent, false);

        ApplyOriginTransform();
        CacheVisualReferences();
        if (slotVisual != null)
            slotVisual.SetActive(true);
        if (cardVisual != null)
            cardVisual.SetActive(false);
        ApplyLockedTint(false);

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Bloqueia a bandeja ao encerrar a rodada. Cards escolhidos continuam
    /// ausentes; somente os cards que sobraram recebem a versão escurecida.
    /// </summary>
    public void LockForSessionEnd()
    {
        CacheComponents();
        CacheVisualReferences();
        StopReturnRoutine(completeReturn: false);
        dragInProgress = false;
        sessionLocked = true;

        if (originParent != null)
            transform.SetParent(originParent, false);
        ApplyOriginTransform();

        if (slotVisual != null)
            slotVisual.SetActive(true);
        if (cardVisual != null)
            cardVisual.SetActive(!accepted);
        ApplyLockedTint(!accepted);

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Reativa o card e o devolve imediatamente ao slot original sem criar clones.
    /// </summary>
    public void ResetToOrigin()
    {
        // Os cards começam inativos na cena. Nesse estado, Awake ainda não foi
        // executado quando o controlador solicita o primeiro reset.
        CacheComponents();
        CacheVisualReferences();
        StopReturnRoutine(completeReturn: true);
        accepted = false;
        sessionLocked = false;
        dragInProgress = false;

        if (!hasOrigin)
            CacheOrigin();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (originParent != null)
            transform.SetParent(originParent, false);

        ApplyOriginTransform();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        if (slotVisual != null)
            slotVisual.SetActive(true);
        if (cardVisual != null)
            cardVisual.SetActive(true);
        ApplyLockedTint(false);
    }

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

    private void CacheVisualReferences()
    {
        if (cardVisual == null || slotVisual == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (slotVisual == null && child.name.StartsWith("Slot", System.StringComparison.Ordinal))
                    slotVisual = child;
                else if (cardVisual == null && child.name.StartsWith("Card", System.StringComparison.Ordinal))
                    cardVisual = child;
            }
        }

        if (cardVisual == null || cardGraphics != null)
            return;

        cardGraphics = cardVisual.GetComponentsInChildren<Graphic>(true);
        originalGraphicColors = new Color[cardGraphics.Length];
        for (int i = 0; i < cardGraphics.Length; i++)
            originalGraphicColors[i] = cardGraphics[i].color;
    }

    private void ApplyLockedTint(bool locked)
    {
        if (cardGraphics == null || originalGraphicColors == null)
            return;

        for (int i = 0; i < cardGraphics.Length; i++)
        {
            Color original = originalGraphicColors[i];
            cardGraphics[i].color = locked
                ? new Color(
                    original.r * lockedTint.r,
                    original.g * lockedTint.g,
                    original.b * lockedTint.b,
                    original.a)
                : original;
        }
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
        dragInProgress = false;
        rectTransform.localScale = hasOrigin ? originLocalScale : Vector3.one;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = !accepted && !sessionLocked;
        canvasGroup.blocksRaycasts = !accepted && !sessionLocked;
    }
}
