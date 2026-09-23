using TMPro;
using UnityEngine;

/// <summary>
/// Exibe o perfil atual no HUB.
/// </summary>
public sealed class HubController : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private AvatarView avatarView;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        var player = PlayerManager.Instance;
        if (player == null || player.Profile == null)
            return;

        var profile = player.Profile;
        if (nameText != null) nameText.text = player.DisplayName;
        if (levelText != null) levelText.text = $"Nível {profile.Level}";
        avatarView?.Apply(profile.Avatar);

        if (pointsText != null)
        {
            // Durante a animação, o PointAnimationManager mantém a propriedade do texto.
            bool animationPendingOrRunning = player.PendingBreakPoints > 0
                || (PointAnimationManager.Instance != null && PointAnimationManager.Instance.IsAnimating);

            if (!animationPendingOrRunning)
            {
                pointsText.text = $"{profile.BreakPoints} PB";
            }
            // Caso contrário, o valor animado seria substituído pelo saldo final.
        }
    }
}
