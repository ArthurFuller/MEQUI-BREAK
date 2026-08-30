using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MinigameCardView : MonoBehaviour
{
    [SerializeField] private Button iconButton;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;

    private MinigameDefinition definition;
    private SceneLoader sceneLoader;

    public void Bind(MinigameDefinition data, SceneLoader loader)
    {
        definition = data;
        sceneLoader = loader;

        icon.sprite = data.Icon;
        titleText.text = data.DisplayName;

        iconButton.onClick.RemoveListener(Play);
        iconButton.onClick.AddListener(Play);
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
                $"'{definition.SceneName}' não está disponível no Build Settings.",
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
}
