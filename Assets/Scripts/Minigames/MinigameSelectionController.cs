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
            if (definitions[i] == null)
                continue;

            MinigameCardView card = Instantiate(cardPrefab, contentRoot);
            card.Bind(definitions[i], sceneLoader);
        }
    }

    public void Back()
    {
        AudioManager.Instance?.PlayClick();
        sceneLoader.Load(backScene);
    }
}
