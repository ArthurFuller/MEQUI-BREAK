using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] private AvatarInteractionReceiver receiver;
    [Header("Rotação visual")]
    [SerializeField] private RectTransform rotatingVisual;
    [SerializeField, Min(0f)] private float clockwiseDegreesPerSecond = 8f;

    private void Awake()
    {
        if (rotatingVisual == null)
            rotatingVisual = transform as RectTransform;
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
            interaction.AcceptDrop();
    }
}
