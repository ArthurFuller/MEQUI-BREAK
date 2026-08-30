using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Semantic haptic feedback used by the mobile UI.
///
/// The public API deliberately describes intent instead of vibration patterns:
/// Selection, LightImpact, Confirm, Reject and Success. Platform-specific code
/// chooses the closest native feedback for iOS/Android.
///
/// All calls respect SettingsManager.VibrationEnabled. In the Unity Editor and
/// unsupported platforms the methods are safe no-ops.
/// </summary>
public static class MequiHaptics
{
    public enum FeedbackType
    {
        Selection,
        LightImpact,
        Confirm,
        Reject,
        Success
    }

    /// <summary>
    /// If SettingsManager is not present (for example when testing a scene
    /// directly on device), haptics default to enabled rather than silently
    /// disabling feedback.
    /// </summary>
    public static bool IsEnabled =>
        SettingsManager.Instance == null || SettingsManager.Instance.VibrationEnabled;

    public static void Selection() => Play(FeedbackType.Selection);
    public static void LightImpact() => Play(FeedbackType.LightImpact);
    public static void Confirm() => Play(FeedbackType.Confirm);
    public static void Reject() => Play(FeedbackType.Reject);
    public static void Success() => Play(FeedbackType.Success);

    public static void Play(FeedbackType type)
    {
        if (!IsEnabled)
            return;

#if UNITY_IOS && !UNITY_EDITOR
        PlayIOS(type);
#elif UNITY_ANDROID && !UNITY_EDITOR
        PlayAndroid(type);
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void MequiHapticSelection();

    [DllImport("__Internal")]
    private static extern void MequiHapticLightImpact();

    [DllImport("__Internal")]
    private static extern void MequiHapticConfirm();

    [DllImport("__Internal")]
    private static extern void MequiHapticReject();

    [DllImport("__Internal")]
    private static extern void MequiHapticSuccess();

    private static void PlayIOS(FeedbackType type)
    {
        switch (type)
        {
            case FeedbackType.Selection:
                MequiHapticSelection();
                break;
            case FeedbackType.LightImpact:
                MequiHapticLightImpact();
                break;
            case FeedbackType.Confirm:
                MequiHapticConfirm();
                break;
            case FeedbackType.Reject:
                MequiHapticReject();
                break;
            case FeedbackType.Success:
                MequiHapticSuccess();
                break;
        }
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void PlayAndroid(FeedbackType type)
    {
        try
        {
            using AndroidJavaClass unityPlayer =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (activity == null)
                return;

            using AndroidJavaObject window =
                activity.Call<AndroidJavaObject>("getWindow");
            using AndroidJavaObject decorView =
                window?.Call<AndroidJavaObject>("getDecorView");

            if (decorView == null)
                return;

            int sdk = GetAndroidSdkInt();
            int constant = ResolveAndroidConstant(type, sdk);
            decorView.Call<bool>("performHapticFeedback", constant);
        }
        catch (System.Exception exception)
        {
            // Haptics must never be able to break a gameplay/UI action.
            Debug.LogWarning($"MequiHaptics: Android haptic could not be played. {exception.Message}");
        }
    }

    private static int GetAndroidSdkInt()
    {
        using AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION");
        return version.GetStatic<int>("SDK_INT");
    }

    private static int GetAndroidHapticConstant(string fieldName, int fallback)
    {
        try
        {
            using AndroidJavaClass constants =
                new AndroidJavaClass("android.view.HapticFeedbackConstants");
            return constants.GetStatic<int>(fieldName);
        }
        catch
        {
            return fallback;
        }
    }

    private static int ResolveAndroidConstant(FeedbackType type, int sdk)
    {
        // Stable legacy constants used as safe fallbacks on older Android.
        const int VirtualKey = 1;
        const int LongPress = 0;
        const int ClockTick = 4;

        switch (type)
        {
            case FeedbackType.Selection:
                return GetAndroidHapticConstant("CLOCK_TICK", ClockTick);

            case FeedbackType.LightImpact:
                return sdk >= 30
                    ? GetAndroidHapticConstant("GESTURE_START", VirtualKey)
                    : GetAndroidHapticConstant("VIRTUAL_KEY", VirtualKey);

            case FeedbackType.Confirm:
                return sdk >= 30
                    ? GetAndroidHapticConstant("CONFIRM", VirtualKey)
                    : GetAndroidHapticConstant("VIRTUAL_KEY", VirtualKey);

            case FeedbackType.Reject:
                return sdk >= 30
                    ? GetAndroidHapticConstant("REJECT", LongPress)
                    : GetAndroidHapticConstant("LONG_PRESS", LongPress);

            case FeedbackType.Success:
                // Android does not expose a dedicated semantic SUCCESS constant;
                // CONFIRM is the closest system-owned feedback.
                return sdk >= 30
                    ? GetAndroidHapticConstant("CONFIRM", VirtualKey)
                    : GetAndroidHapticConstant("VIRTUAL_KEY", VirtualKey);

            default:
                return VirtualKey;
        }
    }
#endif
}
