using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta os controles da interface ao SettingsManager sem assumir a persistência.
/// </summary>
public sealed class SettingsController : MonoBehaviour
{
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private string backScene = "Hub";

    private void Start()
    {
        var settings = SettingsManager.Instance;

        if (settings == null)
            return;

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(settings.MusicVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(settings.SFXVolume);

        if (vibrationToggle != null)
            vibrationToggle.SetIsOnWithoutNotify(settings.VibrationEnabled);
    }

    public void SetMusicVolume(float value)
    {
        SettingsManager.Instance?.SetMusicVolume(value);
    }

    public void SetSFXVolume(float value)
    {
        SettingsManager.Instance?.SetSFXVolume(value);
    }

    public void SetVibration(bool enabled)
    {
        SettingsManager.Instance?.SetVibration(enabled);
    }

    public void Save()
    {
        SettingsManager.Instance?.Save();
        AudioManager.Instance?.PlayConfirm();
    }

    public void Back()
    {
        SettingsManager.Instance?.Save();
        AudioManager.Instance?.PlayClick();

        sceneLoader.Load(backScene);
    }
}
