using DG.Tweening;

/// <summary>
/// Small set of motion defaults that are genuinely shared across the UI.
/// Keep system-specific timings in their own components instead of growing
/// this into a global animation manager.
/// </summary>
public static class UIMotionDefaults
{
    public const bool UseUnscaledTime = true;

    public const float ButtonPressScale = 0.95f;
    public const float ButtonPressDuration = 0.07f;
    public const float ButtonReleaseDuration = 0.10f;
    public const Ease ButtonPressEase = Ease.OutQuad;
    public const Ease ButtonReleaseEase = Ease.OutQuad;
}
