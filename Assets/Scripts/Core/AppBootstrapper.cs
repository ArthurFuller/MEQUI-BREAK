using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public sealed class AppBootstrapper : MonoBehaviour
{
    [Header("Serviços globais")]
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private SettingsManager settingsManager;

    [Header("Cenas iniciais")]
    [FormerlySerializedAs("firstScene")]
    [SerializeField] private string loginScene = "Login";
    [SerializeField] private string hubScene = "HUB";

    private void Awake()
    {
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void Start()
    {
        if (playerManager != null)
            playerManager.Initialize();

        if (settingsManager != null)
            settingsManager.Apply();

        bool hasRegistration = playerManager != null
            && playerManager.HasValidRegistration;

        string targetScene = hasRegistration ? hubScene : loginScene;
        if (!string.IsNullOrWhiteSpace(targetScene))
            SceneManager.LoadScene(targetScene);
    }
}
