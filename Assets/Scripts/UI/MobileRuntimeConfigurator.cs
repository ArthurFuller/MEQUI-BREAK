using UnityEngine;

/// <summary>
/// Applies application-level mobile settings that do not affect orientation or UI layout.
/// Screen orientation is authored in Player Settings so Android/iOS can launch in the
/// correct portrait orientation before the first Unity scene is rendered.
/// </summary>
public static class MobileRuntimeConfigurator
{
    public const int TargetFrameRate = 60;

    public static void ConfigureApplication()
    {
        Application.targetFrameRate = TargetFrameRate;
    }
}
