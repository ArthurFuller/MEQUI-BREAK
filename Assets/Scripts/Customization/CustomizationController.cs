using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edits an avatar in memory and only persists the changes after confirmation.
/// </summary>
public sealed class CustomizationController : MonoBehaviour
{
    private enum AvatarCategory
    {
        Hair,
        Outfit,
        Accessory
    }

    [Header("Scene")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private string profileScene = "Profile";

    [Header("Preview")]
    [SerializeField] private AvatarView avatarPreview;

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

    private readonly AvatarCustomizationData previewData = new AvatarCustomizationData();
    private AvatarCategory selectedCategory = AvatarCategory.Hair;

    private void Start()
    {
        CopyFromSavedAvatar();
        ApplyPreview();
        BindButtons();
        RefreshVisibleOptions();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void SelectHair()
    {
        selectedCategory = AvatarCategory.Hair;
        RefreshVisibleOptions();
    }

    public void SelectOutfit()
    {
        selectedCategory = AvatarCategory.Outfit;
        RefreshVisibleOptions();
    }

    public void SelectAccessory()
    {
        selectedCategory = AvatarCategory.Accessory;
        RefreshVisibleOptions();
    }

    /// <summary>
    /// Assign this method to each option button and pass its zero-based option index.
    /// </summary>
    public void SelectOption(int optionIndex)
    {
        if (optionIndex < 0)
            return;

        switch (selectedCategory)
        {
            case AvatarCategory.Hair:
                previewData.HairIndex = optionIndex;
                break;
            case AvatarCategory.Outfit:
                previewData.OutfitIndex = optionIndex;
                break;
            case AvatarCategory.Accessory:
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
        AudioManager.Instance?.PlayClick();
        sceneLoader?.Load(profileScene);
    }

    public void Back() => Cancel();

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
        SetOptionsVisible(hairOptions, selectedCategory == AvatarCategory.Hair);
        SetOptionsVisible(outfitOptions, selectedCategory == AvatarCategory.Outfit);
        SetOptionsVisible(accessoryOptions, selectedCategory == AvatarCategory.Accessory);
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

    private void BindButtons()
    {
        hairButton?.onClick.AddListener(SelectHair);
        outfitButton?.onClick.AddListener(SelectOutfit);
        accessoryButton?.onClick.AddListener(SelectAccessory);
        confirmButton?.onClick.AddListener(Confirm);
        cancelButton?.onClick.AddListener(Cancel);
    }

    private void UnbindButtons()
    {
        hairButton?.onClick.RemoveListener(SelectHair);
        outfitButton?.onClick.RemoveListener(SelectOutfit);
        accessoryButton?.onClick.RemoveListener(SelectAccessory);
        confirmButton?.onClick.RemoveListener(Confirm);
        cancelButton?.onClick.RemoveListener(Cancel);
    }
}
