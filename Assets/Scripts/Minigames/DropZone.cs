using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DropZone : MonoBehaviour, IDropHandler
{
    [SerializeField] private AvatarInteractionReceiver receiver;

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
