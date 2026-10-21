using UnityEngine;
using UnityEngine.UI;

/// <summary>Contorno transparente desenhado sobre o preenchimento das barras.</summary>
internal static class ComboCrewProgressBorder
{
    private const string BorderName = "ContornoPreto";
    private const string SpritePath = "ComboCrew/order_progress_outline";

    public static void Ensure(Image fill)
    {
        if (fill == null || fill.transform.parent is not RectTransform track) return;
        Transform existing = track.Find(BorderName);
        if (existing != null)
        {
            existing.SetAsLastSibling();
            return;
        }

        Sprite sprite = Resources.Load<Sprite>(SpritePath);
        if (sprite == null)
        {
            Debug.LogError("Combo Crew: sprite do contorno das barras não encontrado.", track);
            return;
        }

        GameObject layer = new GameObject(BorderName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)layer.transform;
        rect.SetParent(track, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image outline = layer.GetComponent<Image>();
        outline.sprite = sprite;
        outline.type = Image.Type.Sliced;
        outline.color = Color.white;
        outline.raycastTarget = false;
        rect.SetAsLastSibling();
    }
}
