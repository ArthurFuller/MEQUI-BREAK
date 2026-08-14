using TMPro;
using UnityEngine;

/// <summary>
/// Displays the current profile on the Hub.
/// </summary>
public sealed class HubController : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text pointsText;

    private void Start() => Refresh();

    public void Refresh()
    {
        var player = PlayerManager.Instance;
        if (player == null || player.Profile == null)
            return;

        var profile = player.Profile;
        if (nameText != null) nameText.text = profile.DisplayName;
        if (roleText != null) roleText.text = profile.Role;
        if (levelText != null) levelText.text = $"Nível {profile.Level}";
        if (pointsText != null) pointsText.text = $"{profile.BreakPoints} PB";
    }
}
