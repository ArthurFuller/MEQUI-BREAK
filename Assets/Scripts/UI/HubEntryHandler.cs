using UnityEngine;
using System;

/// <summary>
/// Checks for pending Break Points when the Hub scene loads and triggers
/// the point animation via PointAnimationManager.
/// </summary>
public sealed class HubEntryHandler : MonoBehaviour
{
    [SerializeField] private PointAnimationManager pointAnimationManager;

    private void Start()
    {
        var player = PlayerManager.Instance;
        if (player == null)
            return;

        int pending = player.PendingBreakPoints;

        if (pending <= 0)
            return;

        if (pointAnimationManager == null)
        {
            Debug.LogError(
                "[HubEntryHandler] PointAnimationManager não atribuído no Inspector.",
                this
            );

            return;
        }

        int finalValue = player.Profile?.BreakPoints ?? 0;
        int baseValue = finalValue - pending;

        // Garante que o texto comece no valor base ANTES da animação
        // (evita flash do valor final 140 → 121...)
        if (pointAnimationManager.PointsLabel != null)
        {
            pointAnimationManager.PointsLabel.text = $"{baseValue} PB";
        }

        // Subscribe to animation complete to clear pending points AFTER animation finishes.
        // This prevents HubController.Refresh() from seeing PendingBreakPoints == 0
        // and overwriting the animating label with the final value.
        Action onComplete = null;
        onComplete = () =>
        {
            player.ClearPendingPoints();
            pointAnimationManager.OnAnimationComplete -= onComplete;
        };
        pointAnimationManager.OnAnimationComplete += onComplete;

        pointAnimationManager.AnimatePoints(baseValue, pending);
    }
}