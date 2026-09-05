using System.Collections;
using UnityEngine;

public sealed class HubEntryHandler : MonoBehaviour
{
    [SerializeField] private PointAnimationManager pointAnimationManager;
    private PlayerManager player;
    private bool isSubscribed;

    private IEnumerator Start()
    {
        player = PlayerManager.Instance;
        if (player == null)
            yield break;

        int pending = player.PendingBreakPoints;

        if (pending <= 0)
            yield break;

        if (pointAnimationManager == null)
        {
            Debug.LogError(
                "[HubEntryHandler] PointAnimationManager não atribuído no Inspector.",
                this
            );

            yield break;
        }

        int finalValue = player.Profile?.BreakPoints ?? 0;
        int baseValue = finalValue - pending;

        if (pointAnimationManager.PointsLabel != null)
            pointAnimationManager.PointsLabel.SetText("{0} PB", baseValue);

        while (SceneLoader.IsTransitionInProgress)
            yield return null;

        // Espera um frame para o layout do HUB fechar antes de animar.
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (this == null || pointAnimationManager == null)
            yield break;

        pending = player.PendingBreakPoints;
        if (pending <= 0)
            yield break;

        finalValue = player.Profile?.BreakPoints ?? 0;
        baseValue = finalValue - pending;

        if (pointAnimationManager.PointsLabel != null)
            pointAnimationManager.PointsLabel.SetText("{0} PB", baseValue);

        SubscribeToCompletion();
        pointAnimationManager.AnimatePoints(baseValue, pending);

        if (!pointAnimationManager.IsAnimating)
            UnsubscribeFromCompletion();
    }

    private void SubscribeToCompletion()
    {
        UnsubscribeFromCompletion();
        pointAnimationManager.OnAnimationComplete += HandleAnimationComplete;
        isSubscribed = true;
    }

    private void HandleAnimationComplete()
    {
        player?.ClearPendingPoints();
        UnsubscribeFromCompletion();
    }

    private void UnsubscribeFromCompletion()
    {
        if (!isSubscribed)
            return;

        if (pointAnimationManager != null)
            pointAnimationManager.OnAnimationComplete -= HandleAnimationComplete;

        isSubscribed = false;
    }

    private void OnDisable()
    {
        UnsubscribeFromCompletion();

        // Ao sair do HUB, descarta PB pendentes para não repetir a animação.
        PlayerManager currentPlayer = player != null ? player : PlayerManager.Instance;
        if (currentPlayer == null || currentPlayer.PendingBreakPoints <= 0)
            return;

        currentPlayer.ClearPendingPoints();

        if (pointAnimationManager?.PointsLabel != null)
            pointAnimationManager.PointsLabel.SetText("{0} PB", currentPlayer.Profile?.BreakPoints ?? 0);
    }
}
