using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Contorno suave e limitado à miniatura do chapéu. As cópias translúcidas
/// ficam atrás da sprite original e acompanham somente sua transparência.
/// </summary>
[RequireComponent(typeof(Graphic))]
public sealed class AvatarSpriteOutline : BaseMeshEffect
{
    [SerializeField, Min(0f)] private float thickness = 4f;

    private readonly List<UIVertex> source = new List<UIVertex>(6);
    private readonly List<UIVertex> outlined = new List<UIVertex>(230);

    public float Thickness
    {
        get => thickness;
        set
        {
            thickness = Mathf.Max(0f, value);
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || thickness <= 0f || vertices.currentVertCount == 0)
            return;

        source.Clear();
        outlined.Clear();
        vertices.GetUIVertexStream(source);

        AppendCopies(24, thickness, 100);
        AppendCopies(12, thickness * .55f, 165);
        outlined.AddRange(source);

        vertices.Clear();
        vertices.AddUIVertexTriangleStream(outlined);
    }

    private void AppendCopies(int count, float radius, byte alpha)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = 2f * Mathf.PI * i / count;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            for (int j = 0; j < source.Count; j++)
            {
                UIVertex vertex = source[j];
                vertex.position += offset;
                vertex.color = new Color32(4, 4, 4,
                    (byte)(vertex.color.a * alpha / 255));
                outlined.Add(vertex);
            }
        }
    }
}
