using System;
using System.Collections.Generic;

[Serializable]
public sealed class PlayerProfileData
{
    public string EmployeeId;
    public string DisplayName;
    public string Role;
    public string StoreId;
    public string Shift;
    public int Level = 1;

    /// <summary>Spendable balance. Goes down when the player buys a customization item.</summary>
    public int BreakPoints;

    /// <summary>Lifetime total earned. Never decreases — Level is derived from this, not from BreakPoints,
    /// so spending PB on cosmetics never lowers the player's Level.</summary>
    public int LifetimeBreakPoints;

    /// <summary>IDs (AvatarCustomizationItem.Id) of items purchased with Break Points. Level-unlocked
    /// items are not stored here — they're derived live from Level instead.</summary>
    public List<string> UnlockedCustomizationIds = new List<string>();

    public AvatarCustomizationData Avatar = new AvatarCustomizationData();
}

[Serializable]
public sealed class AvatarCustomizationData
{
    public int BodyIndex;
    public int HairIndex;
    public int OutfitIndex;
    public int AccessoryIndex;
}
