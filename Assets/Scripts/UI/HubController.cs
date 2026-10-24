using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Exibe o perfil atual no HUB.
/// </summary>
public sealed class HubController : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private AvatarView avatarView;
    [Header("Progresso de nível")]
    [SerializeField] private Image levelTrackImage;
    [SerializeField] private Image levelFillImage;
    [Header("Prévia de level up")]
    [SerializeField] private LevelUpPopupPreview levelUpPopupPreview;

    private RectTransform levelTrackRect;
    private PlayerManager observedPlayer;

    private IEnumerator Start()
    {
        levelTrackRect = levelTrackImage != null ? levelTrackImage.rectTransform : null;
        Refresh();
        levelUpPopupPreview?.Initialize(avatarView);

        observedPlayer = PlayerManager.Instance;
        if (observedPlayer != null)
            observedPlayer.BreakPointsChanged += HandlePointsChanged;

        while (SceneLoader.IsTransitionInProgress)
            yield return null;
        yield return null;

        float waitUntil = Time.unscaledTime + 8f;
        while (observedPlayer != null && observedPlayer.PendingBreakPoints > 0
            && PointAnimationManager.Instance != null && Time.unscaledTime < waitUntil)
            yield return null;

        levelUpPopupPreview?.TryShowPendingLevel();
    }

    private void OnDestroy()
    {
        if (observedPlayer != null)
            observedPlayer.BreakPointsChanged -= HandlePointsChanged;
    }

    private void HandlePointsChanged(int _)
    {
        Refresh();
    }

    private void RefreshLevelBar(PlayerManager player)
    {
        if (levelFillImage == null || levelTrackRect == null) return;
        player.TryGetLevelProgress(out float fraction);
        RectTransform fill = levelFillImage.rectTransform;
        if (levelTrackImage.sprite != null && levelTrackRect.rect.height > 0f)
            levelTrackImage.pixelsPerUnitMultiplier = levelTrackImage.sprite.rect.height
                / (levelTrackRect.rect.height * levelTrackImage.pixelsPerUnit);
        if (levelFillImage.sprite != null && fill.rect.height > 0f)
            levelFillImage.pixelsPerUnitMultiplier = levelFillImage.sprite.rect.height
                / (fill.rect.height * levelFillImage.pixelsPerUnit);
        float availableWidth = Mathf.Max(0f, levelTrackRect.rect.width - 6f);
        float width = Mathf.Min(availableWidth, Mathf.Max(fill.rect.height, availableWidth * Mathf.Clamp01(fraction)));
        fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        levelFillImage.enabled = fraction > 0f;
    }

    public void Refresh()
    {
        var player = PlayerManager.Instance;
        if (player == null || player.Profile == null)
            return;

        var profile = player.Profile;
        if (nameText != null) nameText.text = player.DisplayName;
        if (levelText != null) levelText.text = $"Nível {profile.Level}";
        RefreshLevelBar(player);
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
