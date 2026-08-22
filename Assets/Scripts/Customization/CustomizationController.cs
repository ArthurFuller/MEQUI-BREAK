using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edits an avatar in memory and only persists the changes after confirmation.
/// Locked items (Break Points or Level) are checked against the catalog before
/// a selection is applied; PB items open a short confirmation, Level items just
/// explain what's needed.
///
/// Orchestrates the customization screen, option selection, unlock rules,
/// purchase confirmation and preview persistence.
/// </summary>
public sealed class CustomizationController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private string profileScene = "Profile";

    [Header("Preview")]
    [SerializeField] private AvatarView avatarPreview;

    [Header("Catalog")]
    [Tooltip("Central unlock rules. If left empty, every option is treated as Free.")]
    [SerializeField] private AvatarCustomizationCatalog catalog;

    [Header("Controls")]
    [SerializeField] private Button hairButton;
    [SerializeField] private Button outfitButton;
    [SerializeField] private Button accessoryButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Options")]
    [SerializeField] private GameObject[] hairOptions;
    [SerializeField] private GameObject[] outfitOptions;
    [SerializeField] private GameObject[] accessoryOptions;

    [Header("Feedback (optional)")]
    [SerializeField] private TMP_Text feedbackLabel;

    [Header("Purchase confirmation (optional)")]
    [Tooltip("If left empty, a Break Points purchase is attempted immediately on click instead of asking for confirmation first.")]
    [SerializeField] private GameObject purchaseConfirmPanel;
    [SerializeField] private TMP_Text purchaseConfirmLabel;
    [SerializeField] private Button purchaseConfirmYesButton;
    [SerializeField] private Button purchaseConfirmNoButton;

    [Header("Purchase Panel Animation")]
    [Tooltip("Duration of purchase panel entrance")]
    [SerializeField, Min(0.05f)] private float purchasePanelEnterDuration = 0.25f;

    [Tooltip("Duration of purchase panel exit")]
    [SerializeField, Min(0.05f)] private float purchasePanelExitDuration = 0.2f;

    [Tooltip("Scale factor for purchase panel entrance")]
    [SerializeField, Min(1f)] private float purchasePanelEnterScale = 1.1f;

    private readonly AvatarCustomizationData previewData = new AvatarCustomizationData();
    private AvatarCustomizationCategory selectedCategory = AvatarCustomizationCategory.Hair;
    private AvatarCustomizationItem pendingPurchaseItem;

    // Cached once in Start() instead of calling GetComponent<AvatarOptionButton>()
    // on every option GameObject each time the lock overlays refresh.
    private AvatarOptionButton[] hairButtons;
    private AvatarOptionButton[] outfitButtons;
    private AvatarOptionButton[] accessoryButtons;

    // Cached RectTransform for purchase panel animation
    private RectTransform _purchasePanelRect;

    // Cached CanvasGroup for fade animation
    private CanvasGroup _purchasePanelCanvasGroup;

    private void Start()
    {
        CopyFromSavedAvatar();
        ApplyPreview();
        CacheOptionButtons();
        BindButtons();
        RefreshVisibleOptions();
        RefreshLockVisuals();
        CachePanelReferences();

        // Ensure purchase panel starts hidden
        if (purchaseConfirmPanel != null)
            purchaseConfirmPanel.SetActive(false);

    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    /// <summary>
    /// Caches RectTransform and CanvasGroup for the purchase panel.
    /// </summary>
    private void CachePanelReferences()
    {
        if (purchaseConfirmPanel == null) return;

        _purchasePanelRect = purchaseConfirmPanel.GetComponent<RectTransform>();
        if (_purchasePanelRect == null)
            _purchasePanelRect = purchaseConfirmPanel.GetComponentInParent<RectTransform>();

        _purchasePanelCanvasGroup = purchaseConfirmPanel.GetComponent<CanvasGroup>();
        if (_purchasePanelCanvasGroup == null && _purchasePanelRect != null)
            _purchasePanelCanvasGroup = purchaseConfirmPanel.AddComponent<CanvasGroup>();
    }

    // ============================================================
    // TAB SWITCHING
    // ============================================================

    public void SelectHair()
    {
        selectedCategory = AvatarCustomizationCategory.Hair;
        RefreshVisibleOptions();
    }

    public void SelectOutfit()
    {
        selectedCategory = AvatarCustomizationCategory.Outfit;
        RefreshVisibleOptions();
    }

    public void SelectAccessory()
    {
        selectedCategory = AvatarCustomizationCategory.Accessory;
        RefreshVisibleOptions();
    }

    // ============================================================
    // OPTION SELECTION
    // ============================================================

    /// <summary>
    /// Entry point for swatch buttons. Checks the catalog first: unlocked items
    /// get applied to the preview right away; locked Break Points items open a
    /// purchase confirmation; locked Level items just show what's required.
    /// </summary>
    public void HandleOptionClicked(int optionIndex)
    {
        if (optionIndex < 0)
            return;

        AvatarCustomizationItem item = catalog != null ? catalog.GetItem(selectedCategory, optionIndex) : null;

        if (IsItemUnlocked(item))
        {
            SelectOption(optionIndex);
            return;
        }

        HandleLockedOptionClicked(item, optionIndex);
    }

    /// <summary>
    /// Applies an option index to the in-memory preview without any unlock
    /// check. Called internally once an item is known to be unlocked (or was
    /// just purchased) — kept public in case a caller elsewhere needs to force
    /// a selection (e.g. tests).
    /// </summary>
    public void SelectOption(int optionIndex)
    {
        if (optionIndex < 0)
            return;

        switch (selectedCategory)
        {
            case AvatarCustomizationCategory.Hair:
                previewData.HairIndex = optionIndex;
                break;
            case AvatarCustomizationCategory.Outfit:
                previewData.OutfitIndex = optionIndex;
                break;
            case AvatarCustomizationCategory.Accessory:
                previewData.AccessoryIndex = optionIndex;
                break;
        }

        ApplyPreview();

        // Play selection confirmed animation on the clicked button
        AnimateSelectionConfirmed(optionIndex);

    }

    /// <summary>
    /// Animates the selection confirmed on a specific option button.
    /// </summary>
    private void AnimateSelectionConfirmed(int optionIndex)
    {
        AvatarOptionButton[] buttons = GetButtonsForCategory(selectedCategory);
        if (buttons == null) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].OptionIndex == optionIndex)
            {
                buttons[i].AnimateSelectionConfirmed();
                break;
            }
        }
    }

    private AvatarOptionButton[] GetButtonsForCategory(AvatarCustomizationCategory category)
    {
        return category switch
        {
            AvatarCustomizationCategory.Hair => hairButtons,
            AvatarCustomizationCategory.Outfit => outfitButtons,
            AvatarCustomizationCategory.Accessory => accessoryButtons,
            _ => null
        };
    }

    // ============================================================
    // CONFIRM / CANCEL
    // ============================================================

    public void Confirm()
    {
        AvatarCustomizationData savedAvatar = PlayerManager.Instance?.Profile?.Avatar;
        if (savedAvatar == null)
            return;

        savedAvatar.HairIndex = previewData.HairIndex;
        savedAvatar.OutfitIndex = previewData.OutfitIndex;
        savedAvatar.AccessoryIndex = previewData.AccessoryIndex;

        PlayerManager.Instance.SaveProfile();
        AudioManager.Instance?.PlayConfirm();

        sceneLoader?.Load(profileScene);
    }

    public void Cancel()
    {
        // previewData was never written into PlayerManager.Instance.Profile.Avatar,
        // so simply leaving without calling Confirm() already discards every change
        // made this session and keeps whatever was saved before entering this scene.
        AudioManager.Instance?.PlayClick();

        sceneLoader?.Load(profileScene);
    }

    public void Back() => Cancel();



    // ============================================================
    // PURCHASE FLOW
    // ============================================================

    /// <summary>Confirms the pending Break Points purchase (wire to the Yes button).</summary>
    public void ConfirmPurchase()
    {
        AvatarCustomizationItem item = pendingPurchaseItem;
        pendingPurchaseItem = null;

        if (item == null || PlayerManager.Instance == null)
        {
            purchaseConfirmPanel?.SetActive(false);
            return;
        }

        if (!PlayerManager.Instance.TrySpendBreakPoints(item.BreakPointCost))
        {
            ShowFeedback("PB insuficiente.");
            AnimatePurchasePanelExit();
            return;
        }

        PlayerManager.Instance.UnlockCustomization(item.Id);
        PlayerManager.Instance.SaveProfile();

        // Animate panel exit, then update visuals
        AnimatePurchasePanelExit(() =>
        {
            SelectOption(item.OptionIndex);
            RefreshLockVisuals();
            ShowFeedback($"{DisplayNameOrFallback(item)} desbloqueado!");

            // Animate the newly unlocked button
            AnimateUnlockedButton(item.OptionIndex);
        });
    }

    /// <summary>Cancels the pending Break Points purchase (wire to the No button).</summary>
    public void CancelPurchase()
    {
        pendingPurchaseItem = null;
        AnimatePurchasePanelExit();
    }

    /// <summary>
    /// Animates a newly unlocked button after purchase.
    /// </summary>
    private void AnimateUnlockedButton(int optionIndex)
    {
        AvatarOptionButton[] buttons = GetButtonsForCategory(selectedCategory);
        if (buttons == null) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].OptionIndex == optionIndex)
            {
                buttons[i].AnimateUnlock();
                break;
            }
        }
    }

    // ============================================================
    // PRIVATE HELPERS
    // ============================================================

    private bool IsItemUnlocked(AvatarCustomizationItem item)
    {
        // No catalog entry for this index yet -> treat as unlocked, so the picker
        // stays fully usable while the catalog is still being filled in.
        if (item == null)
            return true;

        switch (item.UnlockType)
        {
            case AvatarUnlockType.Free:
                return true;

            case AvatarUnlockType.BreakPoints:
                return PlayerManager.Instance != null && PlayerManager.Instance.IsCustomizationUnlocked(item.Id);

            case AvatarUnlockType.Level:
                return PlayerManager.Instance?.Profile != null
                    && PlayerManager.Instance.Profile.Level >= item.RequiredLevel;

            default:
                return true;
        }
    }

    private void HandleLockedOptionClicked(AvatarCustomizationItem item, int optionIndex)
    {
        if (item == null)
        {
            // Shouldn't happen (IsItemUnlocked already returns true for a null item),
            // but fall back to applying the selection rather than doing nothing.
            SelectOption(optionIndex);
            return;
        }

        if (item.UnlockType == AvatarUnlockType.Level)
        {
            ShowFeedback($"Disponível no nível {item.RequiredLevel}.");
            return;
        }

        if (item.UnlockType == AvatarUnlockType.BreakPoints)
            OpenPurchaseConfirmation(item);
    }

    private void OpenPurchaseConfirmation(AvatarCustomizationItem item)
    {
        pendingPurchaseItem = item;

        if (purchaseConfirmPanel == null)
        {
            // No confirmation UI wired yet - attempt the purchase immediately so
            // the system stays testable before that panel exists in the scene.
            ConfirmPurchase();
            return;
        }

        if (purchaseConfirmLabel != null)
            purchaseConfirmLabel.text = $"Comprar {DisplayNameOrFallback(item)} por {item.BreakPointCost} PB?";

        AnimatePurchasePanelEnter();
    }

    /// <summary>
    /// Animates the purchase confirmation panel entrance with scale + fade.
    /// </summary>
    private void AnimatePurchasePanelEnter()
    {
        if (purchaseConfirmPanel == null) return;

        // Activate panel
        purchaseConfirmPanel.SetActive(true);

        // Setup CanvasGroup if not cached
        if (_purchasePanelCanvasGroup == null)
        {
            _purchasePanelCanvasGroup = purchaseConfirmPanel.GetComponent<CanvasGroup>();
            if (_purchasePanelCanvasGroup == null)
                _purchasePanelCanvasGroup = purchaseConfirmPanel.AddComponent<CanvasGroup>();
        }

        // Setup RectTransform if not cached
        if (_purchasePanelRect == null)
        {
            _purchasePanelRect = purchaseConfirmPanel.GetComponent<RectTransform>();
            if (_purchasePanelRect == null)
                _purchasePanelRect = purchaseConfirmPanel.GetComponentInParent<RectTransform>();
        }

        // Kill any existing tweens
        _purchasePanelCanvasGroup.DOKill();
        _purchasePanelRect?.DOKill();

        // Set initial state
        _purchasePanelCanvasGroup.alpha = 0f;
        _purchasePanelRect.localScale = Vector3.one * purchasePanelEnterScale;

        // Animate in
        var seq = DOTween.Sequence();
        seq.SetUpdate(UpdateType.Late);

        // Fade in
        seq.Join(_purchasePanelCanvasGroup.DOFade(1f, purchasePanelEnterDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(UpdateType.Late));

        // Scale down from overshoot
        seq.Join(_purchasePanelRect.DOScale(Vector3.one, purchasePanelEnterDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(UpdateType.Late));

        seq.Play();
    }

    /// <summary>
    /// Animates the purchase confirmation panel exit with scale + fade.
    /// </summary>
    private void AnimatePurchasePanelExit(System.Action onComplete = null)
    {
        if (purchaseConfirmPanel == null)
        {
            onComplete?.Invoke();
            return;
        }

        // Setup references if needed
        if (_purchasePanelCanvasGroup == null)
            _purchasePanelCanvasGroup = purchaseConfirmPanel.GetComponent<CanvasGroup>();
        if (_purchasePanelRect == null)
            _purchasePanelRect = purchaseConfirmPanel.GetComponent<RectTransform>();

        if (_purchasePanelCanvasGroup == null || _purchasePanelRect == null)
        {
            purchaseConfirmPanel.SetActive(false);
            onComplete?.Invoke();
            return;
        }

        // Kill any existing tweens
        _purchasePanelCanvasGroup.DOKill();
        _purchasePanelRect.DOKill();

        var seq = DOTween.Sequence();
        seq.SetUpdate(UpdateType.Late);

        // Fade out
        seq.Join(_purchasePanelCanvasGroup.DOFade(0f, purchasePanelExitDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(UpdateType.Late));

        // Scale up slightly before disappearing
        seq.Join(_purchasePanelRect.DOScale(Vector3.one * 0.95f, purchasePanelExitDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(UpdateType.Late));

        seq.OnComplete(() =>
        {
            purchaseConfirmPanel.SetActive(false);
            // Reset scale for next time
            _purchasePanelRect.localScale = Vector3.one;
            onComplete?.Invoke();
        });

        seq.Play();
    }

    private static string DisplayNameOrFallback(AvatarCustomizationItem item)
    {
        return string.IsNullOrWhiteSpace(item.DisplayName) ? item.Id : item.DisplayName;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text = message;

            // Animate feedback text
            var cg = feedbackLabel.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = feedbackLabel.gameObject.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            cg.DOFade(1f, 0.2f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
                .OnComplete(() =>
                {
                    // Fade out after delay
                    cg.DOFade(0f, 0.3f)
                        .SetDelay(1.5f)
                        .SetEase(Ease.InQuad)
                        .SetUpdate(UpdateType.Late);
                });
        }
    }

    private void CopyFromSavedAvatar()
    {
        AvatarCustomizationData savedAvatar = PlayerManager.Instance?.Profile?.Avatar;
        if (savedAvatar == null)
            return;

        previewData.BodyIndex = savedAvatar.BodyIndex;
        previewData.HairIndex = savedAvatar.HairIndex;
        previewData.OutfitIndex = savedAvatar.OutfitIndex;
        previewData.AccessoryIndex = savedAvatar.AccessoryIndex;
    }

    private void ApplyPreview()
    {
        avatarPreview?.Apply(previewData);
    }

    private void RefreshVisibleOptions()
    {
        SetOptionsVisible(hairOptions, selectedCategory == AvatarCustomizationCategory.Hair);
        SetOptionsVisible(outfitOptions, selectedCategory == AvatarCustomizationCategory.Outfit);
        SetOptionsVisible(accessoryOptions, selectedCategory == AvatarCustomizationCategory.Accessory);
    }

    private static void SetOptionsVisible(GameObject[] options, bool visible)
    {
        if (options == null)
            return;

        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != null)
                options[i].SetActive(visible);
        }
    }

    /// <summary>
    /// Updates the lock overlay/label on every swatch button in every category
    /// to match the current profile. Call after Start() and after any purchase.
    /// </summary>
    private void RefreshLockVisuals()
    {
        RefreshCategoryLockVisuals(AvatarCustomizationCategory.Hair, hairButtons);
        RefreshCategoryLockVisuals(AvatarCustomizationCategory.Outfit, outfitButtons);
        RefreshCategoryLockVisuals(AvatarCustomizationCategory.Accessory, accessoryButtons);
    }

    private void RefreshCategoryLockVisuals(AvatarCustomizationCategory category, AvatarOptionButton[] buttons)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            AvatarOptionButton optionButton = buttons[i];
            if (optionButton == null)
                continue;

            AvatarCustomizationItem item = catalog != null ? catalog.GetItem(category, optionButton.OptionIndex) : null;
            bool isLocked = !IsItemUnlocked(item);
            optionButton.SetLocked(isLocked, BuildLockLabel(item));
        }
    }

    private void CacheOptionButtons()
    {
        hairButtons = ExtractButtons(hairOptions);
        outfitButtons = ExtractButtons(outfitOptions);
        accessoryButtons = ExtractButtons(accessoryOptions);
    }

    private static AvatarOptionButton[] ExtractButtons(GameObject[] options)
    {
        if (options == null)
            return System.Array.Empty<AvatarOptionButton>();

        var buttons = new AvatarOptionButton[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != null)
                buttons[i] = options[i].GetComponent<AvatarOptionButton>();
        }

        return buttons;
    }

    private static string BuildLockLabel(AvatarCustomizationItem item)
    {
        if (item == null)
            return string.Empty;

        return item.UnlockType switch
        {
            AvatarUnlockType.BreakPoints => $"{item.BreakPointCost} PB",
            AvatarUnlockType.Level => $"Nv. {item.RequiredLevel}",
            _ => string.Empty
        };
    }

    private void BindButtons()
    {
        hairButton?.onClick.AddListener(SelectHair);
        outfitButton?.onClick.AddListener(SelectOutfit);
        accessoryButton?.onClick.AddListener(SelectAccessory);
        confirmButton?.onClick.AddListener(Confirm);
        cancelButton?.onClick.AddListener(Cancel);
        purchaseConfirmYesButton?.onClick.AddListener(ConfirmPurchase);
        purchaseConfirmNoButton?.onClick.AddListener(CancelPurchase);
    }

    private void UnbindButtons()
    {
        hairButton?.onClick.RemoveListener(SelectHair);
        outfitButton?.onClick.RemoveListener(SelectOutfit);
        accessoryButton?.onClick.RemoveListener(SelectAccessory);
        confirmButton?.onClick.RemoveListener(Confirm);
        cancelButton?.onClick.RemoveListener(Cancel);
        purchaseConfirmYesButton?.onClick.RemoveListener(ConfirmPurchase);
        purchaseConfirmNoButton?.onClick.RemoveListener(CancelPurchase);
    }
}