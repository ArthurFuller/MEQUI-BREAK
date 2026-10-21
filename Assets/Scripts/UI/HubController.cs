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
    [SerializeField] private Sprite levelTrackSprite;
    [SerializeField] private Sprite levelFillSprite;
    [Header("Prévia de level up")]
    [SerializeField] private LevelUpPopupPreview levelUpPopupPreview;

    private RectTransform levelTrackRect;
    private Image levelFillImage;
    private PlayerManager observedPlayer;

    private IEnumerator Start()
    {
        CreateLevelBar();
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

    private void CreateLevelBar()
    {
        if (levelText == null || levelTrackSprite == null || levelFillSprite == null)
            return;

        RectTransform stats = levelText.rectTransform.parent as RectTransform;
        if (stats == null) return;

        GameObject trackObject = new GameObject("LevelProgressTrack", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        levelTrackRect = (RectTransform)trackObject.transform;
        levelTrackRect.SetParent(stats, false);
        levelTrackRect.anchorMin = levelTrackRect.anchorMax = new Vector2(.5f, .5f);
        levelTrackRect.sizeDelta = new Vector2(398f, 16f);
        levelTrackRect.anchoredPosition = new Vector2(0f, -58f);
        Image trackImage = trackObject.GetComponent<Image>();
        trackImage.sprite = levelTrackSprite;
        trackImage.raycastTarget = false;

        GameObject fillObject = new GameObject("LevelProgressFill", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform fillRect = (RectTransform)fillObject.transform;
        fillRect.SetParent(levelTrackRect, false);
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, .5f);
        fillRect.pivot = new Vector2(0f, .5f);
        fillRect.anchoredPosition = new Vector2(3f, 0f);
        fillRect.sizeDelta = new Vector2(0f, 10f);
        levelFillImage = fillObject.GetComponent<Image>();
        levelFillImage.sprite = levelFillSprite;
        levelFillImage.raycastTarget = false;
    }

    private void RefreshLevelBar(PlayerManager player)
    {
        if (levelFillImage == null || levelTrackRect == null) return;
        player.TryGetLevelProgress(out float fraction);
        RectTransform fill = levelFillImage.rectTransform;
        fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            (levelTrackRect.rect.width - 6f) * fraction);
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
