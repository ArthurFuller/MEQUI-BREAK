using UnityEngine;

public sealed class MinigameSelectionController : MonoBehaviour
{
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private MinigameDefinition[] definitions;
    [Tooltip("Cards posicionados e salvos diretamente na Hierarchy.")]
    [SerializeField] private MinigameCardView[] sceneCards;

    private void Start()
    {
        if (definitions == null || sceneLoader == null || sceneCards == null)
            return;

        BindSceneCards();
    }

    private void BindSceneCards()
    {
        for (int i = 0; i < sceneCards.Length; i++)
        {
            MinigameCardView card = sceneCards[i];
            MinigameDefinition definition = i < definitions.Length ? definitions[i] : null;
            if (card == null)
                continue;

            bool available = definition != null
                && !string.IsNullOrWhiteSpace(definition.SceneName)
                && Application.CanStreamedLevelBeLoaded(definition.SceneName);
            card.gameObject.SetActive(available);

            if (!available)
            {
                Debug.LogWarning("Um card de minigame da cena não possui uma definição válida.", card);
                continue;
            }

            card.Bind(definition, sceneLoader);
        }
    }

#if UNITY_EDITOR
    public MinigameDefinition[] EditorDefinitions => definitions;
    public SceneLoader EditorSceneLoader => sceneLoader;

    public void ConfigureSceneCards(MinigameCardView[] cards)
    {
        sceneCards = cards;
    }
#endif

}
