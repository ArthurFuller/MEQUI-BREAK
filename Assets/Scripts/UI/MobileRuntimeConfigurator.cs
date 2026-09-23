using UnityEngine;

/// <summary>
/// Applies application-level mobile settings that do not affect UI layout.
/// Portrait remains the application default. Rush Balance and Combo Crew switch to
/// landscape through SceneLoader/MinigameSessionController when those scenes open.
/// </summary>
public static class MobileRuntimeConfigurator
{
    public const int TargetFrameRate = 60;

    public static void ConfigureApplication()
    {
        Application.targetFrameRate = TargetFrameRate;
    }
}
