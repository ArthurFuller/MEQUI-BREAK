using UnityEngine;

public sealed class AvatarInteractionReceiver : MonoBehaviour
{
    [SerializeField] private AvatarView avatarView;
    [SerializeField] private EnergyStationController stationController;
    [SerializeField] private GameObject itemTemplate;

    public bool Receive(DraggableInteraction interaction)
    {
        if (interaction == null || stationController == null)
            return false;

        avatarView?.PlayReaction(interaction.ReactionTrigger);
        stationController.RegisterInteraction(
            interaction.InteractionId,
            interaction.FeedbackMessage);

        // Update template to the new clone so subsequent drops work
        GameObject newClone = interaction.ConsumeAndRespawn(itemTemplate);
        if (newClone != null)
            itemTemplate = newClone;

        return true;
    }
}
