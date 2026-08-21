using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edits an avatar in memory and only persists the changes after confirmation.
/// Locked items (Break Points or Level) are checked against the catalog before
/// a selection is applied; PB items open a short confirmation, Level items just
/// explain what's needed.
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

    private readonly AvatarCustomizationData previewData = new AvatarCustomizationData();
    private AvatarCustomizationCategory selectedCategory = AvatarCustomizationCategory.Hair;
    private AvatarCustomizationItem pendingPurchaseItem;

    // Cached once in Start() instead of calling GetComponent<AvatarOptionButton>()
    // on every option GameObject each time the lock overlays refresh.
    private AvatarOptionButton[] hairButtons;
    private AvatarOptionButton[] outfitButtons;
    private AvatarOptionButton[] accessoryButtons;

    private void Start()
    {
        CopyFromSavedAvatar();
        ApplyPreview();
        CacheOptionButtons();
        BindButtons();
        RefreshVisibleOptions();
        RefreshLockVisuals();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

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
    }

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

    /// <summary>Confirms the pending Break Points purchase (wire to the Yes button).</summary>
    public void ConfirmPurchase()
    {
        purchaseConfirmPanel?.SetActive(false);

        AvatarCustomizationItem item = pendingPurchaseItem;
        pendingPurchaseItem = null;

        if (item == null || PlayerManager.Instance == null)
            return;

        if (!PlayerManager.Instance.TrySpendBreakPoints(item.BreakPointCost))
        {
            ShowFeedback("PB insuficiente.");
            return;
        }

        PlayerManager.Instance.UnlockCustomization(item.Id);
        PlayerManager.Instance.SaveProfile();

        SelectOption(item.OptionIndex);
        RefreshLockVisuals();
        ShowFeedback($"{DisplayNameOrFallback(item)} desbloqueado!");
    }

    /// <summary>Cancels the pending Break Points purchase (wire to the No button).</summary>
    public void CancelPurchase()
    {
        pendingPurchaseItem = null;
        purchaseConfirmPanel?.SetActive(false);
    }

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

        purchaseConfirmPanel.SetActive(true);
    }

    private static string DisplayNameOrFallback(AvatarCustomizationItem item)
    {
        return string.IsNullOrWhiteSpace(item.DisplayName) ? item.Id : item.DisplayName;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackLabel != null)
            feedbackLabel.text = message;
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
