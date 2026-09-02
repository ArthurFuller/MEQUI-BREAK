using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MinigameCardView : MonoBehaviour
{
    [SerializeField] private Button iconButton;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [Header("Configuração da cena")]
    [SerializeField] private MinigameDefinition definition;
    [SerializeField] private SceneLoader sceneLoader;

    public MinigameDefinition Definition => definition;

    private void Awake()
    {
        ApplyViewAndListener();
    }

    public void Bind(MinigameDefinition data, SceneLoader loader)
    {
        definition = data;
        sceneLoader = loader;

        ApplyViewAndListener();
    }

    private void ApplyViewAndListener()
    {
        if (definition == null)
            return;

        if (icon != null)
            icon.sprite = definition.Icon;

        if (titleText != null)
            titleText.text = definition.DisplayName;

        if (iconButton != null)
        {
            iconButton.onClick.RemoveListener(Play);
            iconButton.onClick.AddListener(Play);
        }
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

    private void OnDestroy()
    {
        if (iconButton != null)
            iconButton.onClick.RemoveListener(Play);
    }

#if UNITY_EDITOR
    public void ConfigureInEditor(MinigameDefinition data, SceneLoader loader)
    {
        definition = data;
        sceneLoader = loader;
        ApplyViewAndListener();
    }
#endif
}
