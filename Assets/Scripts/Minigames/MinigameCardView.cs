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

        iconButton.onClick.RemoveAllListeners();
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
                $"Cannot open minigame '{definition.DisplayName}': scene " +
                $"'{definition.SceneName}' is not available in Build Settings.",
                definition);
            return;
        }

        sceneLoader.Load(definition.SceneName);
    }
}