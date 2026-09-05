using TMPro;
using UnityEngine;

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
            // Durante a animação, o contador é controlado pelo PointAnimationManager.
            bool animationPendingOrRunning = player.PendingBreakPoints > 0
                || (PointAnimationManager.Instance != null && PointAnimationManager.Instance.IsAnimating);

            if (!animationPendingOrRunning)
            {
                pointsText.text = $"{profile.BreakPoints} PB";
            }
        }
    }
}
