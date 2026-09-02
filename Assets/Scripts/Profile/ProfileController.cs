using TMPro;
using UnityEngine;

/// <summary>
/// Exibe os dados do perfil e disponibiliza as ações de navegação.
/// </summary>
public sealed class ProfileController : MonoBehaviour
{
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Interface")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private AvatarView avatarView;

    [Header("Cenas")]
    [SerializeField] private string customizationScene = "Customization";
    [SerializeField] private string hubScene = "Hub";

    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        var player = PlayerManager.Instance;
        var profile = player?.Profile;

        if (profile == null)
            return;

        if (nameText != null)
            nameText.text = player.DisplayName;

        if (levelText != null)
            levelText.text = $"Nível {profile.Level}";

        if (pointsText != null)
            pointsText.text = $"{profile.BreakPoints} PB";

        avatarView?.Apply(profile.Avatar);
    }

    public void OpenCustomization()
    {
        AudioManager.Instance?.PlayClick();
        sceneLoader.Load(customizationScene);
    }

    public void BackToHub()
    {
        AudioManager.Instance?.PlayClick();
        sceneLoader.Load(hubScene);
    }
}
