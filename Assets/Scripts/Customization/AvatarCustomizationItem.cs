using System;

/// <summary>
/// Entrada do catálogo que relaciona categoria, índice visual e regra de desbloqueio.
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
