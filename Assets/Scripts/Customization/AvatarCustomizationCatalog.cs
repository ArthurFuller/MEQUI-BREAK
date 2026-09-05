using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum AvatarHatPreviewOption
{
    [InspectorName("0 - Sem chapéu")] SemChapeu = 0,
    [InspectorName("1 - Boné da equipe")] BoneDaEquipe = 1,
    [InspectorName("2 - Viseira")] Viseira = 2,
    [InspectorName("3 - Gorro")] Gorro = 3,
    [InspectorName("4 - Chapéu bucket")] ChapeuBucket = 4,
    [InspectorName("5 - Chapéu de chef")] ChapeuDeChef = 5,
    [InspectorName("6 - Chapéu de papel")] ChapeuDePapel = 6,
    [InspectorName("7 - Fones")] Fones = 7,
    [InspectorName("8 - Chapéu de festa")] ChapeuDeFesta = 8,
    [InspectorName("9 - Formatura")] Formatura = 9,
    [InspectorName("10 - Chapéu de mago")] ChapeuDeMago = 10,
    [InspectorName("11 - Coroa")] Coroa = 11
}

[CreateAssetMenu(fileName = "AvatarCustomizationCatalog", menuName = "Mequi Break/Avatar Customization Catalog")]
public sealed class AvatarCustomizationCatalog : ScriptableObject
{
    [Header("CHAPÉU MOSTRADO NA PRÉVIA")]
    [Tooltip("Escolha aqui o chapéu que será exibido na Scene View. A troca acontece sem dar Play.")]
    [SerializeField] private AvatarHatPreviewOption chapeuMostradoNaPrevia;

    [Header("AJUSTES MANUAIS - CHAPÉUS")]
    [Tooltip("Abra a lista e selecione um elemento para alterar posição, tamanho e rotação do chapéu.")]
    [FormerlySerializedAs("hairItems")]
    [FormerlySerializedAs("hatItems")]
    [SerializeField] private List<AvatarCustomizationItem> ajustesDosChapeus = new List<AvatarCustomizationItem>();
    [FormerlySerializedAs("outfitItems")]
    [SerializeField] private List<AvatarCustomizationItem> faceItems = new List<AvatarCustomizationItem>();
    [FormerlySerializedAs("accessoryItems")]
    [SerializeField] private List<AvatarCustomizationItem> colorItems = new List<AvatarCustomizationItem>();

    public int ChapeuMostradoNaPrevia => (int)chapeuMostradoNaPrevia;

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= AtualizarAvataresAbertos;
        UnityEditor.EditorApplication.delayCall += AtualizarAvataresAbertos;
    }

    private void AtualizarAvataresAbertos()
    {
        if (this == null)
            return;

        AvatarView[] avatares = Object.FindObjectsByType<AvatarView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (AvatarView avatar in avatares)
        {
            if (avatar != null && avatar.UsaCatalogo(this))
                avatar.AtualizarPreviaNoEditor();
        }
    }
#endif

    public IReadOnlyList<AvatarCustomizationItem> GetItems(AvatarCustomizationCategory category)
    {
        switch (category)
        {
            case AvatarCustomizationCategory.Hat: return ajustesDosChapeus;
            case AvatarCustomizationCategory.Face: return faceItems;
            case AvatarCustomizationCategory.Color: return colorItems;
            default: return System.Array.Empty<AvatarCustomizationItem>();
        }
    }

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

    [ContextMenu("Generate Default 12/12/12 (3 Free + 5 PB + 4 Level)")]
    private void GenerateDefaults()
    {
        ajustesDosChapeus = BuildDefaultCategory(AvatarCustomizationCategory.Hat, "hat");
        faceItems = BuildDefaultCategory(AvatarCustomizationCategory.Face, "face");
        colorItems = BuildDefaultCategory(AvatarCustomizationCategory.Color, "color");
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
