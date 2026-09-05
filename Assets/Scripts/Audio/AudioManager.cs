using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioListener))]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Componentes da cena Boot")]
    [SerializeField] private AudioListener persistentListener;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Clipes em Assets/Audio")]
    [SerializeField] private AudioClip uiClickClip;
    [SerializeField] private AudioClip confirmClip;
    [SerializeField] private AudioClip rewardClip;
    [SerializeField] private AudioClip completionClip;
    [SerializeField] private AudioClip energyDragClip;
    [SerializeField] private AudioClip loginJingleClip;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Min(0f)] private float clickCooldown = 0.04f;

    private bool musicEnabled = true;
    private bool sfxEnabled = true;
    private float nextClickTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        if (persistentListener == null)
            persistentListener = GetComponent<AudioListener>();

        ConfigureListener();
        ConfigureSource(musicSource, musicVolume);
        ConfigureSource(sfxSource, sfxVolume);

        musicEnabled = PlayerPrefs.GetInt("Settings.MusicEnabled", 1) == 1;
        sfxEnabled = PlayerPrefs.GetInt("Settings.SFXEnabled", 1) == 1;
        ApplyEnabledState();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    private void Start()
    {
        KeepOnlyPersistentListener();
        BindButtons(SceneManager.GetActiveScene());
    }

    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        KeepOnlyPersistentListener();
        BindButtons(scene);
    }

    private void ConfigureListener()
    {
        AudioListener.pause = false;
        AudioListener.volume = 1f;

        if (persistentListener != null)
            persistentListener.enabled = true;
    }

    private static void ConfigureSource(AudioSource source, float volume)
    {
        if (source == null)
            return;

        source.enabled = true;
        source.mute = false;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(volume);
        source.ignoreListenerPause = true;
    }

    private void KeepOnlyPersistentListener()
    {
        ConfigureListener();

        AudioListener[] listeners = FindObjectsByType<AudioListener>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (AudioListener listener in listeners)
        {
            if (listener != null && listener != persistentListener)
                listener.enabled = false;
        }
    }

    private void BindButtons(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button == null || button.GetComponent<UIInteractionFeedback>() != null)
                    continue;

                button.onClick.RemoveListener(PlayClick);
                button.onClick.AddListener(PlayClick);
            }
        }
    }

    private void ApplyEnabledState()
    {
        if (musicSource != null)
        {
            musicSource.mute = !musicEnabled;
            musicSource.volume = musicVolume;
            if (!musicEnabled)
                musicSource.Stop();
        }

        if (sfxSource != null)
        {
            sfxSource.mute = !sfxEnabled;
            sfxSource.volume = sfxVolume;
        }
    }

    private void PlayEffect(AudioClip clip)
    {
        if (!sfxEnabled || sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip, 1f);
    }

    public void PlayClick()
    {
        if (!sfxEnabled || uiClickClip == null || Time.unscaledTime < nextClickTime)
            return;

        nextClickTime = Time.unscaledTime + clickCooldown;
        PlayEffect(uiClickClip);
    }

    public void PlayConfirm() => PlayEffect(confirmClip);
    public void PlayReward() => PlayEffect(rewardClip);
    public void PlayCompletion() => PlayEffect(completionClip);
    public void PlayEnergyDrag() => PlayEffect(energyDragClip);

    public void PlayLoginJingle()
    {
        if (musicSource == null || loginJingleClip == null)
            return;

        // O jingle do login toca mesmo se a música estiver desligada nas preferências.
        musicSource.enabled = true;
        musicSource.mute = false;
        musicSource.volume = musicVolume;
        musicSource.Stop();
        musicSource.clip = loginJingleClip;
        musicSource.loop = false;
        musicSource.Play();
    }

    public void SetMusicEnabled(bool enabled)
    {
        musicEnabled = enabled;
        if (musicSource == null)
            return;

        musicSource.mute = !enabled;
        musicSource.volume = musicVolume;
        if (!enabled)
            musicSource.Stop();
    }

    public void SetSFXEnabled(bool enabled)
    {
        sfxEnabled = enabled;
        if (sfxSource != null)
            sfxSource.mute = !enabled;
    }

}
