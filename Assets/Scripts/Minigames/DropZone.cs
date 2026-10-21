using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] private AvatarInteractionReceiver receiver;
    [Header("Rotação visual")]
    [SerializeField] private RectTransform rotatingVisual;
    [SerializeField, Min(0f)] private float clockwiseDegreesPerSecond = 8f;
    [Header("Brilho de aproximação")]
    [SerializeField, Range(.5f, 1f)] private float restingAlpha = .72f;
    [SerializeField, Min(.1f)] private float glowResponse = 5f;
    [SerializeField] private Sprite glowSprite;

    private Image ringImage;
    private Image glowImage;
    private Color originalColor;
    private float currentGlow;
    private float acceptedFlash;

    private void Awake()
    {
        if (rotatingVisual == null)
            rotatingVisual = transform as RectTransform;
        ringImage = GetComponent<Image>();
        if (ringImage != null)
        {
            originalColor = ringImage.color;
            Outline oldOutline = GetComponent<Outline>();
            if (oldOutline != null) oldOutline.enabled = false;
            CreateGlowImage();
            SetGlow(0f);
        }
    }

    private void CreateGlowImage()
    {
        if (glowSprite == null) return;

        GameObject glowObject = new GameObject("ApproachGlow", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform glowRect = (RectTransform)glowObject.transform;
        glowRect.SetParent(transform, false);
        glowRect.anchorMin = new Vector2(.5f, .5f);
        glowRect.anchorMax = new Vector2(.5f, .5f);
        glowRect.pivot = new Vector2(.5f, .5f);
        glowRect.anchoredPosition = Vector2.zero;
        Vector2 ringSpriteSize = ringImage.sprite != null
            ? ringImage.sprite.rect.size : rotatingVisual.rect.size;
        Vector2 haloSpriteSize = glowSprite.rect.size;
        glowRect.sizeDelta = new Vector2(
            rotatingVisual.rect.width * haloSpriteSize.x / ringSpriteSize.x,
            rotatingVisual.rect.height * haloSpriteSize.y / ringSpriteSize.y);

        glowImage = glowObject.GetComponent<Image>();
        glowImage.sprite = glowSprite;
        glowImage.preserveAspect = true;
        glowImage.raycastTarget = false;
    }

    private void Update()
    {
        if (rotatingVisual != null && clockwiseDegreesPerSecond > 0f)
        {
            rotatingVisual.Rotate(
                0f,
                0f,
                -clockwiseDegreesPerSecond * Time.unscaledDeltaTime,
                Space.Self);
        }
        if (ringImage == null || rotatingVisual == null) return;

        DraggableInteraction dragged = receiver != null ? receiver.DraggedInteraction : null;
        float targetGlow = 0f;
        if (dragged != null && dragged.DragRect != null)
        {
            Vector2 local = rotatingVisual.InverseTransformPoint(dragged.DragRect.position);
            float radius = Mathf.Min(rotatingVisual.rect.width, rotatingVisual.rect.height) * .5f;
            float distance = radius > 0f ? local.magnitude / radius : 2f;
            targetGlow = .55f + .45f * (1f - Mathf.InverseLerp(.85f, 3.25f, distance));
        }
        currentGlow = Mathf.MoveTowards(currentGlow, targetGlow,
            glowResponse * Time.unscaledDeltaTime);
        acceptedFlash = Mathf.MoveTowards(acceptedFlash, 0f,
            2.5f * Time.unscaledDeltaTime);
        SetGlow(Mathf.Max(currentGlow, acceptedFlash));
    }

    private void SetGlow(float intensity)
    {
        float amount = Mathf.Clamp01(intensity);
        ringImage.color = new Color(originalColor.r, originalColor.g, originalColor.b,
            originalColor.a * Mathf.Lerp(restingAlpha, 1f, amount));
        if (glowImage != null)
            glowImage.color = new Color(1f, 1f, 1f, amount);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (receiver == null)
            return;

        DraggableInteraction interaction = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<DraggableInteraction>()
            : null;

        if (interaction == null)
            return;

        if (receiver.Receive(interaction))
        {
            interaction.AcceptDrop();
            acceptedFlash = 1f;
        }
    }

    private void OnDisable()
    {
        if (ringImage != null) ringImage.color = originalColor;
        if (glowImage != null) glowImage.color = new Color(1f, 1f, 1f, 0f);
        currentGlow = acceptedFlash = 0f;
    }
}
