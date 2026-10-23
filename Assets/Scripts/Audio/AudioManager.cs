using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Serviço global de áudio. Todos os componentes e clipes são referências
/// serializadas da cena Boot; nada é criado ou carregado em runtime.
/// </summary>
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
    [Tooltip("Opcional. Se vazio, usa o clique como fallback.")]
    [SerializeField] private AudioClip errorClip;
    [Tooltip("Opcional. Se vazio, usa o som de confirmação como fallback.")]
    [SerializeField] private AudioClip readyClip;
    [Tooltip("Opcional. Se vazio, usa o clique como fallback.")]
    [SerializeField] private AudioClip incidentClip;
    [SerializeField] private AudioClip repairClip;
    [SerializeField] private AudioClip stationBreakClip;
    [SerializeField] private AudioClip stationReadyClip;

    [Header("Efeitos por evento")]
    [SerializeField] private AudioClip sessionStartClip;
    [SerializeField] private AudioClip queueAcceptedClip;
    [SerializeField] private AudioClip queueReorderedClip;
    [SerializeField] private AudioClip deliveryClip;
    [SerializeField] private AudioClip orderFailedClip;
    [SerializeField] private AudioClip workerAssignedClip;
    [SerializeField] private AudioClip workerReturnClip;
    [SerializeField] private AudioClip workStageClip;
    [SerializeField] private AudioClip washedClip;
    [SerializeField] private AudioClip breakTickClip;
    [Header("Trilhas dos minigames")]
    [SerializeField] private AudioClip energyMusic;
    [SerializeField] private AudioClip rushMusic;
    [SerializeField] private AudioClip comboMusic;
    [SerializeField, Range(0f, 1f)] private float gameplayMusicVolume = .22f;
    private float nextBreakTickTime;

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
        PlaySceneMusic(SceneManager.GetActiveScene().name);
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
        PlaySceneMusic(scene.name);
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
            musicSource.volume = musicSource.loop ? gameplayMusicVolume : musicVolume;
            if (!musicEnabled)
                musicSource.Stop();
        }

        if (sfxSource != null)
        {
            sfxSource.mute = !sfxEnabled;
            sfxSource.volume = sfxVolume;
        }
    }

    public void PlayEffect(AudioClip clip, float volume = 1f)
    {
        if (!sfxEnabled || sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip, volume);
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
    public void PlayError() => PlayEffect(errorClip != null ? errorClip : uiClickClip);
    public void PlayReady() => PlayEffect(readyClip != null ? readyClip : confirmClip);
    public void PlayIncident() => PlayEffect(incidentClip != null ? incidentClip : uiClickClip);
    public void PlayRepair() => PlayEffect(repairClip != null ? repairClip : confirmClip);
    public void PlayStationBreak() => PlayEffect(stationBreakClip != null ? stationBreakClip
        : incidentClip != null ? incidentClip : uiClickClip, .7f);
    public void PlayStationReady() => PlayEffect(stationReadyClip != null ? stationReadyClip
        : readyClip != null ? readyClip : confirmClip, .7f);

    public void PlaySessionStart() => PlayEffect(sessionStartClip);
    public void PlayQueueAccepted() => PlayEffect(queueAcceptedClip);
    public void PlayQueueReordered() => PlayEffect(queueReorderedClip);
    public void PlayDelivery() => PlayEffect(deliveryClip);
    public void PlayOrderFailed() => PlayEffect(orderFailedClip);
    public void PlayWorkerAssigned() => PlayEffect(workerAssignedClip);
    public void PlayWorkerReturn() => PlayEffect(workerReturnClip);
    public void PlayWorkStage() => PlayEffect(workStageClip, .65f);
    public void PlayWashed() => PlayEffect(washedClip);
    public void PlayBreakTick()
    {
        if (Time.unscaledTime < nextBreakTickTime) return;
        nextBreakTickTime = Time.unscaledTime + .12f;
        PlayEffect(breakTickClip);
    }

    private void PlaySceneMusic(string sceneName)
    {
        if (musicSource == null) return;
        AudioClip next = sceneName == "EnergyStation" ? energyMusic
            : sceneName == "RushBalance2" ? rushMusic
            : sceneName == "ComboCrew" ? comboMusic : null;
        if (next == null)
        {
            // Um jingle curto pode terminar no HUB; uma trilha do jogo deve parar.
            if (musicSource.loop) { musicSource.Stop(); musicSource.clip = null; musicSource.loop = false; }
            return;
        }
        if (musicSource.clip != next)
        {
            musicSource.Stop(); musicSource.clip = next;
        }
        musicSource.loop = true;
        musicSource.volume = gameplayMusicVolume;
        musicSource.mute = !musicEnabled;
        if (musicEnabled && !musicSource.isPlaying) musicSource.Play();
    }

    public void PlayLoginJingle()
    {
        if (musicSource == null || loginJingleClip == null)
            return;

        if (!musicEnabled) return;
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
        musicSource.volume = musicSource.loop ? gameplayMusicVolume : musicVolume;
        if (!enabled) musicSource.Stop();
        else if (musicSource.loop && musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
    }

    public void SetSFXEnabled(bool enabled)
    {
        sfxEnabled = enabled;
        if (sfxSource != null)
            sfxSource.mute = !enabled;
    }

}
