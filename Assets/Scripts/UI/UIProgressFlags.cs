using UnityEngine;

/// <summary>
/// Small persistent UI-only flags. These never own gameplay/progression data;
/// they only remember whether the interface has something new to present.
/// </summary>
public static class UIProgressFlags
{
    private const string CustomizationNewUnlockKey = "UI.Customization.NewUnlock";

    public static bool HasCustomizationNewUnlock =>
        PlayerPrefs.GetInt(CustomizationNewUnlockKey, 0) == 1;

    public static void MarkCustomizationNewUnlock()
    {
        PlayerPrefs.SetInt(CustomizationNewUnlockKey, 1);
        PlayerPrefs.Save();
    }

    public static void ClearCustomizationNewUnlock()
    {
        if (!HasCustomizationNewUnlock)
            return;

        PlayerPrefs.SetInt(CustomizationNewUnlockKey, 0);
        PlayerPrefs.Save();
    }
}
