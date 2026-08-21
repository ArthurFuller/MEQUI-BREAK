using UnityEngine;

/// <summary>
/// Checks for pending Break Points when the Hub scene loads and triggers
/// the point animation via PointAnimationManager.
/// </summary>
public sealed class HubEntryHandler : MonoBehaviour
{
    [SerializeField] private PointAnimationManager pointAnimationManager;
    [SerializeField] private HubController hubController;

    private void Start()
    {
        var player = PlayerManager.Instance;
        if (player == null)
            return;

        int pending = player.PendingBreakPoints;
        if (pending <= 0)
            return;

        player.ClearPendingPoints();

        if (pointAnimationManager != null)
        {
            pointAnimationManager.AnimatePoints(pending);
        }
        else
        {
            Debug.LogError("[HubEntryHandler] PointAnimationManager não atribuído no Inspector.", this);
        }
    }
}
