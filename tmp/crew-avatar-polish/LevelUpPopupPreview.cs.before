using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the HUB level-up preview. All visual objects are authored in the
/// HUB hierarchy; this component only fills their data and animates them.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelUpPopupPreview : MonoBehaviour
{
    [Serializable]
    private sealed class RewardSlot
    {
        public GameObject root;
        public CanvasGroup canvasGroup;
        public Image icon;
        public TMP_Text itemName;
    }

    [Serializable]
    private sealed class CelebrationParticle
    {
        public RectTransform rect;
        public CanvasGroup canvasGroup;
    }

    [Header("Ações")]
    [SerializeField] private Button simulateButton;
    [SerializeField] private Button continueButton;

    [Header("Painel")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private CanvasGroup popupGroup;
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private RectTransform levelMedal;
    [SerializeField] private TMP_Text levelBadgeText;
    [SerializeField] private AvatarView avatarView;

    [Header("Progresso")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text previousLevelText;
    [SerializeField] private TMP_Text nextLevelText;
    [SerializeField] private RectTransform progressTrack;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text rewardHeadingText;

    [Header("Recompensas")]
    [SerializeField] private RewardSlot[] rewardSlots = Array.Empty<RewardSlot>();
    [SerializeField] private Sprite partyHatRewardIcon;
    [SerializeField] private Sprite sailorHatRewardIcon;
    [SerializeField] private Sprite starEyesRewardIcon;
    [SerializeField] private Sprite colorSwatchSprite;
    [SerializeField] private CelebrationParticle[] celebrationParticles = Array.Empty<CelebrationParticle>();

    [Header("Animação")]
    [SerializeField, Min(0.1f)] private float progressDuration = 0.8f;
    [SerializeField, Min(0f)] private float rewardStagger = 0.12f;

    private const float ProgressInset = 6f;

    private Sequence activeSequence;
    private Vector2[] celebrationTargets = Array.Empty<Vector2>();
    private Vector2 popupPanelRestingPosition;
    private bool isOpen;
    private bool initialized;
    private int shownLevel;

    private void Awake()
    {
        UpdateSimulatorVisibility();
    }

    public void Initialize(AvatarView view)
    {
        ResolveSceneReferences(view);
        UpdateSimulatorVisibility();

        List<string> missingReferences = new();
        AddMissing(missingReferences, continueButton, nameof(continueButton));
        AddMissing(missingReferences, popupRoot, nameof(popupRoot));
        AddMissing(missingReferences, popupGroup, nameof(popupGroup));
        AddMissing(missingReferences, popupPanel, nameof(popupPanel));
        AddMissing(missingReferences, levelMedal, nameof(levelMedal));
        AddMissing(missingReferences, levelBadgeText, nameof(levelBadgeText));
        AddMissing(missingReferences, progressTrack, nameof(progressTrack));
        AddMissing(missingReferences, progressFill, nameof(progressFill));
        AddMissing(missingReferences, avatarView, nameof(avatarView));
        AddMissing(missingReferences, avatarView != null ? avatarView.Catalog : null, "AvatarView.Catalog");
        AddMissing(missingReferences, titleText, nameof(titleText));
        AddMissing(missingReferences, previousLevelText, nameof(previousLevelText));
        AddMissing(missingReferences, nextLevelText, nameof(nextLevelText));
        AddMissing(missingReferences, rewardHeadingText, nameof(rewardHeadingText));

        if (missingReferences.Count > 0)
        {
            Debug.LogWarning(
                $"Level-up preview disabled. Missing scene references: {string.Join(", ", missingReferences)}.",
                this);
            return;
        }

        Canvas.ForceUpdateCanvases();
        popupPanelRestingPosition = popupPanel.anchoredPosition;
        celebrationTargets = new Vector2[celebrationParticles != null ? celebrationParticles.Length : 0];
        for (int i = 0; i < celebrationTargets.Length; i++)
        {
            CelebrationParticle particle = celebrationParticles[i];
            if (particle != null && particle.rect != null)
                celebrationTargets[i] = particle.rect.anchoredPosition;
        }

        continueButton.onClick.RemoveListener(ClosePopup);
        continueButton.onClick.AddListener(ClosePopup);

        ResetPopupVisuals();
        popupRoot.SetActive(false);
        initialized = true;
    }

    private void ResolveSceneReferences(AvatarView view)
    {
        avatarView = view != null ? view : FindSceneComponent<AvatarView>(null);
        simulateButton = Resolve(simulateButton, "SimularLevelUpButton");
        continueButton = Resolve(continueButton, "ContinueButton");
        popupRoot = popupRoot != null ? popupRoot : FindSceneObject("LevelUpPopupRoot");
        popupGroup = Resolve(popupGroup, popupRoot);
        popupPanel = Resolve(popupPanel, "PopupPanel");
        levelMedal = Resolve(levelMedal, "LevelMedal");
        levelBadgeText = Resolve(levelBadgeText, "LevelMedalValue");
        if (levelBadgeText == null && levelMedal != null)
            levelBadgeText = levelMedal.GetComponentInChildren<TMP_Text>(true);
        progressTrack = Resolve(progressTrack, "ProgressTrack");
        progressFill = Resolve(progressFill, "ProgressFill");
        titleText = Resolve(titleText, "LevelTitle");
        previousLevelText = Resolve(previousLevelText, "PreviousLevel");
        nextLevelText = Resolve(nextLevelText, "NextLevel");
        rewardHeadingText = Resolve(rewardHeadingText, "RewardHeading");
    }

    private void UpdateSimulatorVisibility()
    {
        if (simulateButton != null)
            simulateButton.gameObject.SetActive(false);
    }

    private T FindSceneComponent<T>(string objectName) where T : Component
    {
        Scene targetScene = gameObject.scene;
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        return components.FirstOrDefault(component => component != null
            && component.gameObject.scene == targetScene
            && (string.IsNullOrEmpty(objectName) || component.gameObject.name == objectName));
    }

    private GameObject FindSceneObject(string objectName)
    {
        Scene targetScene = gameObject.scene;
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        Transform match = transforms.FirstOrDefault(candidate => candidate != null
            && candidate.gameObject.scene == targetScene
            && candidate.gameObject.name == objectName);
        return match != null ? match.gameObject : null;
    }

    private Button Resolve(Button current, string objectName) =>
        current != null ? current : FindSceneComponent<Button>(objectName);

    private TMP_Text Resolve(TMP_Text current, string objectName) =>
        current != null ? current : FindSceneComponent<TMP_Text>(objectName);

    private Image Resolve(Image current, string objectName) =>
        current != null ? current : FindSceneComponent<Image>(objectName);

    private RectTransform Resolve(RectTransform current, string objectName) =>
        current != null ? current : FindSceneComponent<RectTransform>(objectName);

    private CanvasGroup Resolve(CanvasGroup current, GameObject root) =>
        current != null || root == null ? current : root.GetComponent<CanvasGroup>();

    private static void AddMissing<T>(List<string> missing, T reference, string label)
        where T : UnityEngine.Object
    {
        if (reference == null)
            missing.Add(label);
    }

    private void OnDestroy()
    {
        activeSequence?.Kill();

        if (continueButton != null)
            continueButton.onClick.RemoveListener(ClosePopup);
    }

    public void TryShowPendingLevel()
    {
        if (!initialized || isOpen)
            return;

        PlayerProfileData profile = PlayerManager.Instance?.Profile;
        if (profile == null || profile.LastPresentedLevel >= profile.Level)
            return;

        int previousLevel = Mathf.Max(1, profile.LastPresentedLevel);
        int nextLevel = previousLevel + 1;
        shownLevel = nextLevel;

        const float panelRevealDuration = 0.3f;
        isOpen = true;
        activeSequence?.Kill();

        List<AvatarCustomizationItem> rewards = GetLevelRewards(nextLevel);
        BindRewards(rewards);
        ResetPopupVisuals();
        levelBadgeText.SetText("{0}", previousLevel);
        previousLevelText.SetText("NÍVEL {0}", previousLevel);
        nextLevelText.SetText("NÍVEL {0}", nextLevel);
        popupRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        popupGroup.interactable = false;
        popupGroup.blocksRaycasts = true;

        activeSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        activeSequence.Join(popupGroup.DOFade(1f, 0.22f).SetEase(Ease.OutQuad));
        activeSequence.Join(popupPanel.DOScale(1f, panelRevealDuration).SetEase(Ease.OutCubic));
        activeSequence.Join(popupPanel.DOAnchorPos(popupPanelRestingPosition, panelRevealDuration)
            .SetEase(Ease.OutCubic));
        Vector2 fullProgressSize = progressFill.rectTransform.sizeDelta;
        fullProgressSize.x = Mathf.Max(0f, progressTrack.rect.width - ProgressInset * 2f);
        fullProgressSize.y = Mathf.Max(0f, progressTrack.rect.height - ProgressInset * 2f);
        progressFill.rectTransform.anchoredPosition = new Vector2(ProgressInset, 0f);
        progressFill.rectTransform.sizeDelta = fullProgressSize;
        progressFill.fillAmount = 0f;
        activeSequence.Append(progressFill
            .DOFillAmount(1f, progressDuration)
            .SetEase(Ease.OutCubic));

        float celebrationTime = panelRevealDuration + progressDuration;
        activeSequence.AppendCallback(PlayLevelUpFeedback);
        // The badge background and outline are one Image. Its RectTransform
        // pivot is centered, so one smooth scale pulse expands symmetrically;
        // the number inherits the same motion from its parent.
        activeSequence.Insert(celebrationTime,
            levelMedal.DOScale(1.18f, 0.36f).SetEase(Ease.OutCubic));
        activeSequence.InsertCallback(celebrationTime + 0.3f, () =>
        {
            if (levelBadgeText != null)
                levelBadgeText.SetText("{0}", nextLevel);
        });
        activeSequence.Insert(celebrationTime + 0.36f,
            levelMedal.DOScale(1f, 0.26f).SetEase(Ease.OutSine));
        AddCelebrationTweens(activeSequence, celebrationTime);

        for (int i = 0; i < rewardSlots.Length; i++)
        {
            RewardSlot slot = rewardSlots[i];
            if (slot == null || slot.root == null || !slot.root.activeSelf || slot.canvasGroup == null)
                continue;

            float rewardRevealStart = panelRevealDuration + progressDuration + i * rewardStagger;
            activeSequence.Insert(rewardRevealStart,
                slot.canvasGroup.DOFade(1f, 0.18f));
            activeSequence.Insert(rewardRevealStart,
                slot.root.transform.DOScale(1.025f, 0.24f).SetEase(Ease.OutCubic));
            activeSequence.Insert(rewardRevealStart + 0.28f,
                slot.root.transform.DOScale(1f, 0.12f).SetEase(Ease.OutSine));
        }

        activeSequence.OnComplete(() =>
        {
            if (popupGroup != null)
                popupGroup.interactable = true;
            if (continueButton != null)
                continueButton.interactable = true;
        });
    }

    private void PlayLevelUpFeedback()
    {
        AudioManager.Instance?.PlayReward();
        MequiHaptics.Success();
    }

    private void AddCelebrationTweens(Sequence sequence, float startTime)
    {
        if (celebrationParticles == null || celebrationTargets == null)
            return;

        Vector2 origin = levelMedal.anchoredPosition;
        for (int i = 0; i < celebrationParticles.Length; i++)
        {
            CelebrationParticle particle = celebrationParticles[i];
            if (particle == null || particle.rect == null || particle.canvasGroup == null)
                continue;

            RectTransform rect = particle.rect;
            CanvasGroup group = particle.canvasGroup;
            rect.anchoredPosition = origin;
            rect.localScale = Vector3.one * 0.42f;
            rect.localRotation = Quaternion.identity;
            group.alpha = 0f;

            float spin = (i % 2 == 0 ? 1f : -1f) * (120f + 35f * (i % 3));
            sequence.Insert(startTime, group.DOFade(0.9f, 0.16f));
            sequence.Insert(startTime, rect.DOAnchorPos(celebrationTargets[i], 0.68f).SetEase(Ease.OutCubic));
            sequence.Insert(startTime, rect.DOScale(0.82f, 0.36f).SetEase(Ease.OutCubic));
            sequence.Insert(startTime, rect.DOLocalRotate(new Vector3(0f, 0f, spin), 0.68f, RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic));
            sequence.Insert(startTime + 0.52f, group.DOFade(0f, 0.36f));
            sequence.Insert(startTime + 0.52f, rect.DOScale(0.5f, 0.36f).SetEase(Ease.InQuad));
        }
    }

    private List<AvatarCustomizationItem> GetLevelRewards(int level)
    {
        AvatarCustomizationCatalog catalog = avatarView.Catalog;
        var rewards = new List<AvatarCustomizationItem>();

        foreach (AvatarCustomizationCategory category in Enum.GetValues(typeof(AvatarCustomizationCategory)))
        {
            IReadOnlyList<AvatarCustomizationItem> items = catalog.GetItems(category);
            for (int i = 0; i < items.Count; i++)
            {
                AvatarCustomizationItem item = items[i];
                if (item != null && item.UnlockType == AvatarUnlockType.Level && item.RequiredLevel == level)
                    rewards.Add(item);
            }
        }

        return rewards.OrderBy(item => item.Category).ThenBy(item => item.OptionIndex).ToList();
    }

    private void BindRewards(IReadOnlyList<AvatarCustomizationItem> rewards)
    {
        if (rewardSlots == null || rewardSlots.Length == 0)
        {
            rewardHeadingText.text = "NOVOS DESBLOQUEIOS";
            return;
        }

        int visibleCount = Mathf.Min(rewards.Count, rewardSlots.Length);
        rewardHeadingText.text = visibleCount == 1 ? "NOVO DESBLOQUEIO" : "NOVOS DESBLOQUEIOS";

        for (int i = 0; i < rewardSlots.Length; i++)
        {
            RewardSlot slot = rewardSlots[i];
            if (slot == null || slot.root == null)
                continue;

            bool hasReward = i < visibleCount;
            slot.root.SetActive(hasReward);
            if (slot.canvasGroup != null)
                slot.canvasGroup.alpha = 0f;
            if (!hasReward || slot.icon == null || slot.itemName == null)
                continue;

            AvatarCustomizationItem item = rewards[i];
            slot.itemName.text = item.DisplayName;
            slot.icon.preserveAspect = true;
            slot.icon.raycastTarget = false;

            if (item.Category == AvatarCustomizationCategory.Color)
            {
                slot.icon.sprite = colorSwatchSprite;
                slot.icon.color = avatarView.TryGetCustomizationColor(item.OptionIndex, out Color color)
                    ? color
                    : Color.white;
            }
            else
            {
                slot.icon.sprite = GetRewardIcon(item);
                slot.icon.color = Color.white;
            }
        }
    }

    private Sprite GetRewardIcon(AvatarCustomizationItem item)
    {
        if (item.Category == AvatarCustomizationCategory.Hat)
        {
            if (item.OptionIndex == 8 && partyHatRewardIcon != null)
                return partyHatRewardIcon;
            if (item.OptionIndex == 17 && sailorHatRewardIcon != null)
                return sailorHatRewardIcon;
        }

        if (item.Category == AvatarCustomizationCategory.Face
            && item.OptionIndex == 8 && starEyesRewardIcon != null)
            return starEyesRewardIcon;

        return avatarView.GetCustomizationSprite(item.Category, item.OptionIndex);
    }

    private void ResetPopupVisuals()
    {
        popupGroup.alpha = 0f;
        popupGroup.interactable = false;
        popupGroup.blocksRaycasts = false;
        popupPanel.localScale = Vector3.one * 0.96f;
        popupPanel.anchoredPosition = popupPanelRestingPosition + Vector2.down * 18f;
        levelMedal.localScale = Vector3.one;
        levelBadgeText.transform.localScale = Vector3.one;
        levelBadgeText.text = "1";
        Vector2 progressSize = progressFill.rectTransform.sizeDelta;
        progressSize.x = Mathf.Max(0f, progressTrack.rect.width - ProgressInset * 2f);
        progressSize.y = Mathf.Max(0f, progressTrack.rect.height - ProgressInset * 2f);
        progressFill.rectTransform.anchoredPosition = new Vector2(ProgressInset, 0f);
        progressFill.rectTransform.sizeDelta = progressSize;
        progressFill.fillAmount = 0f;
        titleText.text = "SUBIU DE NÍVEL!";
        previousLevelText.text = "NÍVEL 1";
        nextLevelText.text = "NÍVEL 2";
        continueButton.interactable = false;

        if (rewardSlots == null)
            return;

        foreach (RewardSlot slot in rewardSlots)
        {
            if (slot?.canvasGroup != null)
                slot.canvasGroup.alpha = 0f;
            if (slot?.root != null)
                slot.root.transform.localScale = Vector3.one * 0.96f;
        }

        if (celebrationParticles == null)
            return;

        Vector2 particleOrigin = levelMedal.anchoredPosition;
        foreach (CelebrationParticle particle in celebrationParticles)
        {
            if (particle == null)
                continue;
            if (particle.canvasGroup != null)
                particle.canvasGroup.alpha = 0f;
            if (particle.rect != null)
            {
                particle.rect.anchoredPosition = particleOrigin;
                particle.rect.localScale = Vector3.one * 0.2f;
                particle.rect.localRotation = Quaternion.identity;
            }
        }
    }

    private void ClosePopup()
    {
        if (!isOpen || popupRoot == null)
            return;

        isOpen = false;
        activeSequence?.Kill();
        popupGroup.interactable = false;
        activeSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        activeSequence.Join(popupGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad));
        activeSequence.Join(popupPanel.DOScale(0.985f, 0.2f).SetEase(Ease.InQuad));
        activeSequence.OnComplete(() =>
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);
            if (PlayerManager.Instance?.MarkLevelPresented(shownLevel) == true)
                TryShowPendingLevel();
        });
    }
}
