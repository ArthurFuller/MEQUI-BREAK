using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Four animated donut segments on the scene's existing chart object.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DashboardDonutGraphic : MaskableGraphic
{
    [SerializeField, Range(0.1f, 0.9f)] private float innerRadiusRatio = 0.70f;
    [SerializeField] private Color[] segmentColors =
    {
        new Color32(0x86, 0xc5, 0xd4, 0xff),
        new Color32(0xff, 0xcf, 0x32, 0xff),
        new Color32(0xb2, 0xcb, 0x82, 0xff),
        new Color32(0xd3, 0x9d, 0x85, 0xff)
    };

    private readonly float[] shares = { 0.25f, 0.25f, 0.25f, 0.25f };

    public void SetShares(float[] values)
    {
        if (values == null || values.Length != 4) return;
        float sum = 0f;
        for (int i = 0; i < 4; i++) sum += Mathf.Max(0f, values[i]);
        if (sum <= 0f) { Array.Clear(shares, 0, shares.Length); SetVerticesDirty(); return; }
        for (int i = 0; i < 4; i++) shares[i] = Mathf.Max(0f, values[i]) / sum;
        SetVerticesDirty();
    }

    public void SetPalette(Color[] colors)
    {
        if (colors == null || colors.Length != 4) return;
        segmentColors = (Color[])colors.Clone();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect bounds = GetPixelAdjustedRect();
        float outer = Mathf.Min(bounds.width, bounds.height) * 0.5f - 0.75f;
        float inner = outer * innerRadiusRatio;
        Vector2 center = bounds.center;
        float angle = 90f;

        for (int section = 0; section < 4; section++)
        {
            float sweep = shares[section] * 360f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(sweep / 2f));
            Color solid = segmentColors[section];
            Color transparent = new Color(solid.r, solid.g, solid.b, 0f);
            for (int i = 0; i < steps; i++)
            {
                float a = angle - sweep * i / steps;
                float b = angle - sweep * (i + 1) / steps;
                AddStrip(helper, center, inner + 0.5f, outer - 0.5f, a, b, solid, solid);
                AddStrip(helper, center, outer - 0.5f, outer + 0.5f, a, b, solid, transparent);
                AddStrip(helper, center, inner - 0.5f, inner + 0.5f, a, b, transparent, solid);
            }
            angle -= sweep;
        }
    }

    private static void AddStrip(VertexHelper helper, Vector2 center, float radiusA, float radiusB,
        float angleA, float angleB, Color colorA, Color colorB)
    {
        int index = helper.currentVertCount;
        helper.AddVert(Vertex(center, radiusA, angleA, colorA));
        helper.AddVert(Vertex(center, radiusB, angleA, colorB));
        helper.AddVert(Vertex(center, radiusB, angleB, colorB));
        helper.AddVert(Vertex(center, radiusA, angleB, colorA));
        helper.AddTriangle(index, index + 1, index + 2);
        helper.AddTriangle(index + 2, index + 3, index);
    }

    private static UIVertex Vertex(Vector2 center, float radius, float angle, Color color)
    {
        float radians = angle * Mathf.Deg2Rad;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        vertex.color = color;
        return vertex;
    }
}
