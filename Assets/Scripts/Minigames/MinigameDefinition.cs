using UnityEngine;

[CreateAssetMenu(
    fileName = "MinigameDefinition",
    menuName = "Mequi Break/Minigame Definition"
)]
public sealed class MinigameDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private string sceneName;

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string SceneName => sceneName;
}
