using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central, data-driven source of truth for which avatar customization options
/// are free, purchasable with Break Points, or unlocked by Level. Keeping this
/// as one asset avoids hardcoding unlock rules inside buttons or controllers.
/// </summary>
[CreateAssetMenu(fileName = "AvatarCustomizationCatalog", menuName = "Mequi Break/Avatar Customization Catalog")]
public sealed class AvatarCustomizationCatalog : ScriptableObject
{
    [SerializeField] private List<AvatarCustomizationItem> hairItems = new List<AvatarCustomizationItem>();
    [SerializeField] private List<AvatarCustomizationItem> outfitItems = new List<AvatarCustomizationItem>();
    [SerializeField] private List<AvatarCustomizationItem> accessoryItems = new List<AvatarCustomizationItem>();

    public IReadOnlyList<AvatarCustomizationItem> GetItems(AvatarCustomizationCategory category)
    {
        switch (category)
        {
            case AvatarCustomizationCategory.Hair: return hairItems;
            case AvatarCustomizationCategory.Outfit: return outfitItems;
            case AvatarCustomizationCategory.Accessory: return accessoryItems;
            default: return System.Array.Empty<AvatarCustomizationItem>();
        }
    }

    /// <summary>
    /// Returns the catalog entry for a given category/option index, or null if
    /// none is configured yet (treated as Free by the controller, so the picker
    /// stays usable while the catalog is still being filled in).
    /// </summary>
    public AvatarCustomizationItem GetItem(AvatarCustomizationCategory category, int optionIndex)
    {
        IReadOnlyList<AvatarCustomizationItem> items = GetItems(category);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].OptionIndex == optionIndex)
                return items[i];
        }

        return null;
    }

    /// <summary>
    /// Fills all three categories with the 12-item layout proposed in the plan:
    /// indices 0-2 Free, 3-7 Break Points (40/60/80/100/120), 8-11 Level (2/3/4/5).
    /// Right-click the asset (or the gear icon) and choose this to seed sensible
    /// defaults instead of creating 36 entries by hand — adjust costs/levels
    /// afterward as needed.
    /// </summary>
    [ContextMenu("Generate Default 12/12/12 (3 Free + 5 PB + 4 Level)")]
    private void GenerateDefaults()
    {
        hairItems = BuildDefaultCategory(AvatarCustomizationCategory.Hair, "hair");
        outfitItems = BuildDefaultCategory(AvatarCustomizationCategory.Outfit, "outfit");
        accessoryItems = BuildDefaultCategory(AvatarCustomizationCategory.Accessory, "accessory");
    }

    private static List<AvatarCustomizationItem> BuildDefaultCategory(AvatarCustomizationCategory category, string idPrefix)
    {
        int[] breakPointCosts = { 40, 60, 80, 100, 120 };
        int[] requiredLevels = { 2, 3, 4, 5 };

        var items = new List<AvatarCustomizationItem>(12);
        for (int index = 0; index <= 11; index++)
        {
            var item = new AvatarCustomizationItem
            {
                Id = $"{idPrefix}_{index:00}",
                Category = category,
                OptionIndex = index,
                DisplayName = $"{idPrefix} {index}"
            };

            if (index <= 2)
            {
                item.UnlockType = AvatarUnlockType.Free;
            }
            else if (index <= 7)
            {
                item.UnlockType = AvatarUnlockType.BreakPoints;
                item.BreakPointCost = breakPointCosts[index - 3];
            }
            else
            {
                item.UnlockType = AvatarUnlockType.Level;
                item.RequiredLevel = requiredLevels[index - 8];
            }

            items.Add(item);
        }

        return items;
    }
}
