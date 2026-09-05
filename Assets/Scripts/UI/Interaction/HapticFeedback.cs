using UnityEngine;

/// <summary>
/// Centralized haptic feedback utility for mobile devices.
/// Respects SettingsManager.VibrationEnabled setting.
/// Structured for future iOS UIImpactFeedbackGenerator / Android VibrationEffect integration.
/// </summary>
public static class HapticFeedback
{
    public enum Type
    {
        Light,       // Light tap - selection, minor interactions
        Medium,      // Medium tap - confirmations, primary actions
        Heavy,       // Heavy tap - major confirmations, purchases
        Selection,   // Selection change - carousel, tabs, options
        Success,     // Success outcome - purchase complete, level up
        Warning,     // Warning - destructive action confirmation
        Error        // Error/Rejection - invalid input, locked item
    }

    /// <summary>
    /// Plays haptic feedback if vibration is enabled in settings.
    /// Uses Handheld.Vibrate() as baseline - platform-specific implementations
    /// can override this via native plugins for intensity differentiation.
    /// </summary>
    public static void Play(Type type)
    {
        // Check if vibration is enabled in settings
        if (SettingsManager.Instance != null && !SettingsManager.Instance.VibrationEnabled)
            return;

#if UNITY_IOS || UNITY_ANDROID
        // Unity's Handheld.Vibrate() triggers the system default haptic
        // On iOS: uses UIImpactFeedbackGenerator (light/medium/heavy not directly controllable via this API)
        // On Android: uses Vibrator.vibrate() with default pattern
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// Plays haptic feedback with explicit intensity control (for platforms supporting advanced haptics).
    /// Falls back to Handheld.Vibrate() on unsupported platforms.
    /// This method is structured for future native plugin integration.
    /// </summary>
    public static void PlayAdvanced(Type type)
    {
        if (SettingsManager.Instance != null && !SettingsManager.Instance.VibrationEnabled)
            return;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        // Mantém o comportamento atual nos dispositivos móveis suportados.
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// Map HapticFeedback.Type to iOS UIImpactFeedbackStyle (for native plugin reference).
    /// </summary>
    public static int GetIosImpactStyle(Type type)
    {
        return type switch
        {
            Type.Light => 0,       // UIImpactFeedbackStyle.Light
            Type.Medium => 1,      // UIImpactFeedbackStyle.Medium
            Type.Heavy => 2,       // UIImpactFeedbackStyle.Heavy
            _ => 1                 // Default to Medium
        };
    }

    /// <summary>
    /// Map HapticFeedback.Type to Android VibrationEffect (for native plugin reference).
    /// </summary>
    public static int GetAndroidEffectType(Type type)
    {
        return type switch
        {
            Type.Light => 0,       // EFFECT_CLICK
            Type.Selection => 0,   // EFFECT_CLICK
            Type.Medium => 1,      // EFFECT_TICK
            Type.Heavy => 5,       // EFFECT_HEAVY_CLICK
            Type.Success => 2,     // Custom pattern
            Type.Warning => 3,     // Custom pattern
            Type.Error => 4,       // Custom pattern
            _ => 1                 // Default to TICK
        };
    }
}
