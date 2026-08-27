using UnityEngine;

public sealed class MinigameSelectionController : MonoBehaviour
{
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private MinigameDefinition[] definitions;
    [SerializeField] private MinigameCardView cardPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private string backScene = "Hub";

    private void Start()
    {
        if (definitions == null || cardPrefab == null || contentRoot == null)
            return;

        for (int i = 0; i < definitions.Length; i++)
        {
            MinigameDefinition definition = definitions[i];
            if (definition == null)
                continue;

            // A definition can exist before its minigame scene is implemented.
            // Do not create a clickable card that can only lead to a broken load.
            if (string.IsNullOrWhiteSpace(definition.SceneName)
                || !Application.CanStreamedLevelBeLoaded(definition.SceneName))
            {
                Debug.LogWarning(
                    $"Minigame '{definition.DisplayName}' was skipped because scene " +
                    $"'{definition.SceneName}' is not available in Build Settings.",
                    definition);
                continue;
            }

            MinigameCardView card = Instantiate(cardPrefab, contentRoot);
            card.Bind(definition, sceneLoader);
        }
    }

    public void Back()
    {
        AudioManager.Instance?.PlayClick();
        sceneLoader.Load(backScene);
    }
}
