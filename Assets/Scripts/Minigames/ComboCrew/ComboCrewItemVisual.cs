using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusa a primeira imagem do item e esconde as camadas antigas da ilustração.</summary>
internal static class ComboCrewItemVisual
{
    public static void Set(GameObject slot, Sprite sprite)
    {
        if (slot == null || sprite == null) return;
        Image image = slot.GetComponent<Image>();
        if (image == null)
        {
            Transform root = slot.transform;
            if (root.childCount == 0) return;
            image = root.GetChild(0).GetComponent<Image>();
            if (image == null) return;
            for (int i = 1; i < root.childCount; i++) root.GetChild(i).gameObject.SetActive(false);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
    }
}
