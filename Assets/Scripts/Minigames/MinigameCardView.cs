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
        sceneLoader.Load(definition.SceneName);
    }
}