using UnityEngine;

public sealed class AppBootstrapper : MonoBehaviour
{
    [Header("Global Services")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PointsService pointsService;
    [SerializeField] private SettingsManager settingsManager;
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private LocalStorage localStorage;
    [SerializeField] private EventLogger eventLogger;

    [Header("Startup")]
    [SerializeField] private string firstScene = "Login";

    private void Start()
    {
        if (playerManager != null)
            playerManager.Initialize();

        if (settingsManager != null)
            settingsManager.Apply();

        if (sceneLoader != null)
            sceneLoader.Load(firstScene);
    }
}
