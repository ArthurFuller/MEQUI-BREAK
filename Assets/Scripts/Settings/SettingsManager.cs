using UnityEngine;

public sealed class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    private const string MusicEnabledKey = "Settings.MusicEnabled";
    private const string SFXEnabledKey = "Settings.SFXEnabled";
    private const string VibrationKey = "Settings.Vibration";
    private const string NotificationsEnabledKey = "Settings.NotificationsEnabled";
    private const string EndOfShiftReminderKey = "Settings.EndOfShiftReminder";

    public bool MusicEnabled { get; private set; }
    public bool SFXEnabled { get; private set; }
    public bool VibrationEnabled { get; private set; }
    public bool NotificationsEnabled { get; private set; }
    public bool EndOfShiftReminderEnabled { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Load();
    }

    private void Load()
    {
        // Não migra volume antigo em zero para os novos toggles.
        MusicEnabled = PlayerPrefs.HasKey(MusicEnabledKey)
            ? PlayerPrefs.GetInt(MusicEnabledKey) == 1
            : true;
        SFXEnabled = PlayerPrefs.HasKey(SFXEnabledKey)
            ? PlayerPrefs.GetInt(SFXEnabledKey) == 1
            : true;
        VibrationEnabled = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        NotificationsEnabled = PlayerPrefs.GetInt(NotificationsEnabledKey, 0) == 1;
        EndOfShiftReminderEnabled = PlayerPrefs.GetInt(EndOfShiftReminderKey, 0) == 1;
    }

    public void Apply()
    {
        AudioManager.Instance?.SetMusicEnabled(MusicEnabled);
        AudioManager.Instance?.SetSFXEnabled(SFXEnabled);
    }

    public void SetMusicEnabled(bool enabled)
    {
        MusicEnabled = enabled;
        SaveBool(MusicEnabledKey, enabled);
        AudioManager.Instance?.SetMusicEnabled(enabled);
    }

    public void SetSFXEnabled(bool enabled)
    {
        SFXEnabled = enabled;
        SaveBool(SFXEnabledKey, enabled);
        AudioManager.Instance?.SetSFXEnabled(enabled);
    }

    public void SetVibration(bool enabled)
    {
        VibrationEnabled = enabled;
        SaveBool(VibrationKey, enabled);
    }

    public void SetNotificationsEnabled(bool enabled)
    {
        NotificationsEnabled = enabled;
        SaveBool(NotificationsEnabledKey, enabled);

        if (!enabled && EndOfShiftReminderEnabled)
            SetEndOfShiftReminderEnabled(false);
    }

    public void SetEndOfShiftReminderEnabled(bool enabled)
    {
        EndOfShiftReminderEnabled = enabled && NotificationsEnabled;
        SaveBool(EndOfShiftReminderKey, EndOfShiftReminderEnabled);
    }

    private static void SaveBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }

}
