using DG.Tweening;
using UnityEngine;

/// <summary>
/// ScriptableObject configuration for interaction animation presets.
/// Allows designers to configure press/release animation parameters per interaction type.
/// </summary>
[CreateAssetMenu(
    fileName = "InteractionAnimationConfig",
    menuName = "UI/Interaction Animation Config",
    order = 0)]
public sealed class InteractionAnimationConfig : ScriptableObject
{
    [Header("Press Animation")]
    [Tooltip("Scale multiplier applied when pointer goes down (e.g., 0.95 = 5% shrink).")]
    [Range(0.8f, 1f)]
    public float pressScale = 0.95f;

    [Tooltip("Duration of the press scale animation in seconds.")]
    [Min(0f)]
    public float pressDuration = 0.08f;

    [Tooltip("Easing function for press animation.")]
    public Ease pressEase = Ease.OutQuad;

    [Header("Release Animation")]
    [Tooltip("Overshoot scale multiplier on release (e.g., 1.05 = 5% grow beyond 1.0).")]
    [Range(1f, 1.2f)]
    public float releaseOvershootScale = 1.05f;

    [Tooltip("Duration of the overshoot animation in seconds.")]
    [Min(0f)]
    public float releaseOvershootDuration = 0.10f;

    [Tooltip("Easing function for overshoot (typically OutBack for bounce).")]
    public Ease releaseOvershootEase = Ease.OutBack;

    [Tooltip("Duration of the settle animation from overshoot to 1.0 in seconds.")]
    [Min(0f)]
    public float releaseSettleDuration = 0.05f;

    [Tooltip("Easing function for settle animation (typically OutCubic).")]
    public Ease releaseSettleEase = Ease.OutCubic;

    [Header("Selection Confirmation (Optional)")]
    [Tooltip("Pop scale for selection confirmation (used by toggle/selectable items).")]
    [Range(1f, 1.3f)]
    public float selectionPopScale = 1.15f;

    [Tooltip("Duration of selection pop animation.")]
    [Min(0f)]
    public float selectionPopDuration = 0.12f;

    [Tooltip("Punch scale amount for selection confirmation.")]
    [Min(0f)]
    public float selectionPunchAmount = 0.15f;

    [Tooltip("Duration of punch animation.")]
    [Min(0f)]
    public float selectionPunchDuration = 0.2f;

    [Header("Locked/Disabled Feedback")]
    [Tooltip("Shake strength for locked items.")]
    [Min(0f)]
    public float lockedShakeStrength = 15f;

    [Tooltip("Shake duration for locked items.")]
    [Min(0f)]
    public float lockedShakeDuration = 0.3f;

    [Tooltip("Number of shake vibrations.")]
    [Min(0)]
    public int lockedShakeVibrato = 10;

    [Header("Audio")]
    [Tooltip("Play click sound on press.")]
    public bool playClickOnPress = true;

    [Tooltip("Play confirm sound on release (for primary actions).")]
    public bool playConfirmOnRelease = false;

    [Header("Advanced")]
    [Tooltip("If true, animations use LateUpdate for smoother frame alignment.")]
    public bool useLateUpdate = true;
}