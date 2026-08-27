using DG.Tweening;
using UnityEngine;

/// <summary>
/// ScriptableObject configuration for panel/popup enter and exit animations.
/// Allows designers to configure animation parameters per panel type.
/// </summary>
[CreateAssetMenu(
    fileName = "PanelAnimationConfig",
    menuName = "UI/Panel Animation Config",
    order = 1)]
public sealed class PanelAnimationConfig : ScriptableObject
{
    [Header("Enter Animation")]
    [Tooltip("Duration of the enter animation in seconds.")]
    [Min(0.05f)]
    public float enterDuration = 0.35f;

    [Tooltip("Easing function for enter animation.")]
    public Ease enterEase = Ease.OutCubic;

    [Tooltip("Slide offset in pixels (positive = from bottom, negative = from top).")]
    public float slideOffset = 800f;

    [Tooltip("If true, panel slides from off-screen.")]
    public bool useSlide = true;

    [Tooltip("If true, panel fades in from 0 alpha.")]
    public bool useFade = true;

    [Header("Exit Animation")]
    [Tooltip("Duration of the exit animation in seconds. Typically faster than enter.")]
    [Min(0.05f)]
    public float exitDuration = 0.25f;

    [Tooltip("Easing function for exit animation.")]
    public Ease exitEase = Ease.InCubic;

    [Tooltip("If true, panel slides off-screen on exit.")]
    public bool useSlideOnExit = true;

    [Header("Audio")]
    [Tooltip("Play sound when panel opens.")]
    public bool playOpenSound = false;

    [Tooltip("Play sound when panel closes.")]
    public bool playCloseSound = false;
}