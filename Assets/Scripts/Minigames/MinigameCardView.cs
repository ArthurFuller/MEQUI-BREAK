using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class MinigameCardView : MonoBehaviour
{
    [SerializeField] private Button button;
    [Header("Configuração da cena")]
    [SerializeField] private MinigameDefinition definition;
    [SerializeField] private SceneLoader sceneLoader;

    public MinigameDefinition Definition => definition;

    private void Awake()
    {
        ApplyListener();
    }

    private void OnEnable()
    {
        RefreshAvailability();
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.EnergyStationAvailabilityChanged += RefreshAvailability;
    }

    public void Bind(MinigameDefinition data, SceneLoader loader)
    {
        definition = data;
        sceneLoader = loader;

        ApplyListener();
    }

    private void ApplyListener()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
            return;

        button.onClick.RemoveListener(Play);

        if (definition != null)
            button.onClick.AddListener(Play);

        RefreshAvailability();
    }

    public void RefreshAvailability()
    {
        if (button == null || definition == null)
            return;

        bool isEnergyStation = string.Equals(
            definition.SceneName,
            "EnergyStation",
            System.StringComparison.OrdinalIgnoreCase);

        button.interactable = !isEnergyStation
            || PlayerManager.Instance == null
            || PlayerManager.Instance.CanPlayEnergyStation;
    }

    private void Play()
    {
        if (definition == null || sceneLoader == null)
            return;

        if (string.IsNullOrWhiteSpace(definition.SceneName)
            || !Application.CanStreamedLevelBeLoaded(definition.SceneName))
        {
            Debug.LogWarning(
                $"Não foi possível abrir o minigame '{definition.DisplayName}': a cena " +
                $"'{definition.SceneName}' não está disponível nas configurações de compilação.",
                definition);
            return;
        }

        sceneLoader.Load(definition.SceneName);
    }

    private void OnDisable()
    {
        if (PlayerManager.Instance != null)
            PlayerManager.Instance.EnergyStationAvailabilityChanged -= RefreshAvailability;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Play);
    }

#if UNITY_EDITOR
    public void ConfigureInEditor(MinigameDefinition data, SceneLoader loader)
    {
        definition = data;
        sceneLoader = loader;
        ApplyListener();
    }
#endif
}
