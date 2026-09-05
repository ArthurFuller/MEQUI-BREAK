using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

[Serializable]
public sealed class PlayerProfileData
{
    public bool RegistrationCompleted;

    public int OnboardingStep;

    public string DisplayName;

    public string StoreId;

    public string Shift;

    public string LastEnergyStationCompletionDate;

    public string LastEnergyStationCompletionShift;

    public int Level = 1;

    public int BreakPoints;

    public bool BreakPointsInitialized;

    public int LifetimeBreakPoints;

    public List<string> UnlockedCustomizationIds = new List<string>();

    public AvatarCustomizationData Avatar = new AvatarCustomizationData();
}

[Serializable]
public sealed class AvatarCustomizationData
{
    [FormerlySerializedAs("HairIndex")]
    public int HatIndex;

    [FormerlySerializedAs("OutfitIndex")]
    public int FaceIndex;

    [FormerlySerializedAs("AccessoryIndex")]
    public int ColorIndex;
}
