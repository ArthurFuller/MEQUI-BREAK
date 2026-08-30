using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fonte central das regras que definem opções gratuitas, compráveis com
/// Break Points ou desbloqueadas por nível.
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
    /// Retorna a entrada correspondente à categoria e ao índice informados.
    /// Sem entrada configurada, o controlador considera a opção gratuita.
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
    /// Preenche as três categorias com 12 itens: índices 0 a 2 gratuitos,
    /// 3 a 7 por Break Points e 8 a 11 por nível. Os valores podem ser ajustados depois.
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
