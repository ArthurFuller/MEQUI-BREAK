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

    [Header("Fluxo inicial")]
    [Tooltip("Quando ativado, o Boot sempre abre a tela de Login, mesmo que já exista um cadastro válido salvo.")]
    [SerializeField] private bool alwaysShowLoginOnBoot = true;

    private void Awake()
    {
        // O projeto usa um único inicializador persistente; o limite é aplicado
        // uma vez antes de qualquer troca de cena.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        DontDestroyOnLoad(transform.root.gameObject);

        // O guia é um único componente persistente; ele observa as cenas sem
        // substituir os ouvintes já configurados nos botões.
        if (GetComponent<FirstRunGuideController>() == null)
            gameObject.AddComponent<FirstRunGuideController>();
    }

    private void Start()
    {
        if (playerManager != null)
            playerManager.Initialize();

        if (settingsManager != null)
            settingsManager.Apply();

        bool hasRegistration = playerManager != null
            && playerManager.HasValidRegistration;

        string targetScene = alwaysShowLoginOnBoot || !hasRegistration
            ? loginScene
            : hubScene;
        if (!string.IsNullOrWhiteSpace(targetScene))
            SceneManager.LoadScene(targetScene);
    }
}
