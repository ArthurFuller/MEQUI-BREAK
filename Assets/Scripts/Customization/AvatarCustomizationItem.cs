using System;

/// <summary>
/// One entry in the AvatarCustomizationCatalog: which avatar option it maps to
/// (Category + OptionIndex, matching AvatarView's sprite arrays and
/// CustomizationController's swatch arrays) and how it is unlocked.
/// </summary>
[Serializable]
public sealed class AvatarCustomizationItem
{
    public string Id;
    public AvatarCustomizationCategory Category;
    public int OptionIndex;
    public AvatarUnlockType UnlockType = AvatarUnlockType.Free;
    public int BreakPointCost;
    public int RequiredLevel = 1;
    public string DisplayName;
}
