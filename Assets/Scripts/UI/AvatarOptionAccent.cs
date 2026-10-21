using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pequenos traços vetoriais para as opções do avatar.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AvatarOptionAccent : MaskableGraphic
{
    public enum AccentKind { None, NoHat, ColorBorder }

    [SerializeField] private AccentKind kind;

    public static void SetFaceAccent(Image face, AvatarView avatar)
    {
        if (face == null || avatar == null)
            return;

        string spriteName = face.sprite != null ? face.sprite.name : string.Empty;
        bool determined = spriteName == "face_determined";
        bool sunglasses = spriteName == "face_sunglasses";

        // Os olhos dessas duas artes estavam abaixo da linha usada pelos outros rostos.
        float eyeOffset = determined ? .083f : sunglasses ? .067f : 0f;
        float imageSize = Mathf.Min(face.rectTransform.rect.width, face.rectTransform.rect.height);
        float shift = imageSize * eyeOffset;
        if (shift > 0f)
            face.rectTransform.anchoredPosition += new Vector2(0f, shift);

        // Desativa bocas criadas por versões anteriores; as bocas agora fazem
        // parte das próprias sprites dos rostos.
        Transform oldMouth = face.transform.Find("Boca");
        if (oldMouth != null) oldMouth.gameObject.SetActive(false);
    }

    public void SetKind(AccentKind value)
    {
        kind = value;
        color = Color.black;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width, rect.height);
        if (scale <= 0f) return;

        Vector2 Point(float x, float y) => new Vector2(
            rect.xMin + rect.width * x, rect.yMin + rect.height * y);

        switch (kind)
        {
            case AccentKind.NoHat:
                Ring(vh, Point(.5f, .5f), scale * .36f, scale * .034f);
                Stroke(vh, Point(.38f, .38f), Point(.62f, .62f), scale * .04f);
                Stroke(vh, Point(.38f, .62f), Point(.62f, .38f), scale * .04f);
                break;
            case AccentKind.ColorBorder:
                Ring(vh, Point(.5f, .5f), scale * .475f, scale * .05f);
                break;
        }
    }

    private void Ring(VertexHelper vh, Vector2 center, float radius, float thickness)
    {
        const int segments = 96;
        float feather = Mathf.Min(1.25f, thickness * .2f);
        float[] radii = { radius + thickness * .5f + feather,
            radius + thickness * .5f, radius - thickness * .5f,
            radius - thickness * .5f - feather };
        Color32 solid = color;
        Color32 transparent = new Color32(solid.r, solid.g, solid.b, 0);
        int first = vh.currentVertCount;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            for (int j = 0; j < 4; j++)
                AddVertex(vh, center + direction * radii[j], j == 0 || j == 3 ? transparent : solid);

            if (i == 0) continue;
            int previous = first + (i - 1) * 4;
            int current = first + i * 4;
            for (int j = 0; j < 3; j++)
            {
                vh.AddTriangle(previous + j, current + j, current + j + 1);
                vh.AddTriangle(previous + j, current + j + 1, previous + j + 1);
            }
        }
    }

    private void Stroke(VertexHelper vh, Vector2 start, Vector2 end, float thickness)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * .5f);
        int index = vh.currentVertCount;
        AddVertex(vh, start - normal, color);
        AddVertex(vh, start + normal, color);
        AddVertex(vh, end + normal, color);
        AddVertex(vh, end - normal, color);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Color32 tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = tint;
        vertex.position = position;
        vh.AddVert(vertex);
    }
}
