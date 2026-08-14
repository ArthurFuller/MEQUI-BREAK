using System;

[Serializable]
public sealed class PlayerProfileData
{
    public string EmployeeId;
    public string DisplayName;
    public string Role;
    public string StoreId;
    public string Shift;
    public int Level = 1;
    public int BreakPoints;
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
