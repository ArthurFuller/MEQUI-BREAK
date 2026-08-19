using UnityEngine;

public sealed class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    private const string MusicVolumeKey = "Settings.MusicVolume";
    private const string SFXVolumeKey = "Settings.SFXVolume";
    private const string VibrationKey = "Settings.Vibration";

    public float MusicVolume { get; private set; }
    public float SFXVolume { get; private set; }
    public bool VibrationEnabled { get; private set; }

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

    public void Load()
    {
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        SFXVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);
        VibrationEnabled = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
    }

    public void Apply()
    {
        AudioManager.Instance?.SetMusicVolume(MusicVolume);
        AudioManager.Instance?.SetSFXVolume(SFXVolume);
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        AudioManager.Instance?.SetMusicVolume(MusicVolume);
    }

    public void SetSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        AudioManager.Instance?.SetSFXVolume(SFXVolume);
    }

    public void SetVibration(bool enabled) => VibrationEnabled = enabled;

    public void Save()
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.SetFloat(SFXVolumeKey, SFXVolume);
        PlayerPrefs.SetInt(VibrationKey, VibrationEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
