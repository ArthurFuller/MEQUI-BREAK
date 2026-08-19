using TMPro;
using UnityEngine;

/// <summary>
/// Displays read-only profile information and exposes navigation actions.
/// </summary>
public sealed class ProfileController : MonoBehaviour
{
    [SerializeField] private SceneLoader sceneLoader;

    [Header("UI")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text employeeIdText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private AvatarView avatarView;

    [Header("Scenes")]
    [SerializeField] private string customizationScene = "Customization";
    [SerializeField] private string hubScene = "Hub";

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        var profile = PlayerManager.Instance?.Profile;

        if (profile == null)
            return;

        if (nameText != null)
            nameText.text = profile.DisplayName;

        if (employeeIdText != null)
            employeeIdText.text = profile.EmployeeId;

        if (roleText != null)
            roleText.text = profile.Role;

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
