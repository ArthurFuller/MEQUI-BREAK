using UnityEngine;

public sealed class AvatarInteractionReceiver : MonoBehaviour
{
    [SerializeField] private AvatarView avatarView;
    [SerializeField] private EnergyStationController stationController;
    public DraggableInteraction DraggedInteraction => stationController != null
        ? stationController.GetDraggedInteraction() : null;

    public bool Receive(DraggableInteraction interaction)
    {
        if (interaction == null || stationController == null)
            return false;

        if (!stationController.TryRegisterInteraction(
                interaction.InteractionId,
                interaction.FeedbackMessage))
            return false;

        avatarView?.PlayReaction(interaction.ReactionTrigger);
        return true;
    }
}
