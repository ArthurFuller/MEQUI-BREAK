using UnityEngine;

public sealed class AvatarInteractionReceiver : MonoBehaviour
{
    [SerializeField] private AvatarView avatarView;
    [SerializeField] private EnergyStationController stationController;

    public bool Receive(DraggableInteraction interaction)
    {
        if (interaction == null || stationController == null)
            return false;

        avatarView?.PlayReaction(interaction.ReactionTrigger);
        stationController.RegisterInteraction(
            interaction.InteractionId,
            interaction.FeedbackMessage);

        interaction.Respawn();

        return true;
    }
}
