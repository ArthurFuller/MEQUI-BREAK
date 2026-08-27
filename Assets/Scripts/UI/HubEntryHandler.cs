using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Checks for pending Break Points when the Hub scene loads and triggers
/// the point animation via PointAnimationManager.
///
/// When Hub is being entered through SceneLoader's additive slide transition,
/// the PB animation waits until the scene transition is fully finished. This
/// prevents the flying PB icons from moving together with the Hub transition
/// root and keeps their path visually anchored to the final Hub layout.
/// </summary>
public sealed class HubEntryHandler : MonoBehaviour
{
    [SerializeField] private PointAnimationManager pointAnimationManager;

    private IEnumerator Start()
    {
        var player = PlayerManager.Instance;
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

        // Keep the HUD on the pre-reward value while the Hub is sliding in.
        // The flying PB icons themselves must only begin after the transition.
        if (pointAnimationManager.PointsLabel != null)
        {
            pointAnimationManager.PointsLabel.text = $"{baseValue} PB";
        }

        // Hub is loaded additively before the slide finishes, so Start() runs
        // while the whole Hub Canvas is still travelling. Waiting here keeps the
        // PB flight animation completely separate from the scene transition.
        while (SceneLoader.IsTransitionInProgress)
            yield return null;

        // Give the incoming scene one frame after SceneLoader resets its roots so
        // RectTransform positions/layout are final before target positions are read.
        yield return null;
        Canvas.ForceUpdateCanvases();

        // The scene could have been unloaded while waiting (for example through
        // an external navigation request). Avoid touching destroyed references.
        if (this == null || pointAnimationManager == null)
            yield break;

        // Re-read pending points after the wait in case another system changed
        // the player state before the transition completed.
        pending = player.PendingBreakPoints;
        if (pending <= 0)
            yield break;

        finalValue = player.Profile?.BreakPoints ?? 0;
        baseValue = finalValue - pending;

        if (pointAnimationManager.PointsLabel != null)
            pointAnimationManager.PointsLabel.text = $"{baseValue} PB";

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
