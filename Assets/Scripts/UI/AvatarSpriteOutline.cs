using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Contorno preto uniforme em unidades do Canvas, independente da escala do acessório.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Graphic))]
public sealed class AvatarSpriteOutline : BaseMeshEffect
{
    [SerializeField, Min(0f), Tooltip("Espessura visual em unidades do Canvas. Mesmo valor para corpos, chapéus e miniaturas.")]
    private float thickness = 10f;
    private readonly List<UIVertex> source = new List<UIVertex>(6);
    private readonly List<UIVertex> outlined = new List<UIVertex>(600);
    private Vector3 axisX = Vector3.right, axisY = Vector3.up;
    public float Thickness
    {
        get => thickness;
        set { thickness = Mathf.Max(0f, value); if (graphic != null) graphic.SetVerticesDirty(); }
    }
    private void CanvasAxes(out Vector3 x, out Vector3 y)
    {
        Canvas canvas = graphic != null ? graphic.canvas : null;
        Transform reference = canvas != null ? canvas.rootCanvas.transform : null;
        x = transform.InverseTransformVector(reference != null
            ? reference.TransformVector(Vector3.right) : Vector3.right);
        y = transform.InverseTransformVector(reference != null
            ? reference.TransformVector(Vector3.up) : Vector3.up);
    }
    private void LateUpdate()
    {
        CanvasAxes(out Vector3 x, out Vector3 y);
        if ((x - axisX).sqrMagnitude + (y - axisY).sqrMagnitude < .000001f) return;
        axisX = x; axisY = y;
        if (graphic != null) graphic.SetVerticesDirty();
    }
    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || thickness <= 0f || vertices.currentVertCount == 0) return;
        source.Clear(); outlined.Clear(); vertices.GetUIVertexStream(source);
        CanvasAxes(out axisX, out axisY);
        // Amostragem circular densa para não formar quinas ou lados mais finos.
        AppendCopies(64, thickness);
        AppendCopies(32, thickness * .5f);
        outlined.AddRange(source);
        vertices.Clear(); vertices.AddUIVertexTriangleStream(outlined);
    }
    private void AppendCopies(int count, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = 2f * Mathf.PI * i / count;
            Vector3 offset = (axisX * Mathf.Cos(angle) + axisY * Mathf.Sin(angle)) * radius;
            for (int j = 0; j < source.Count; j++)
            {
                UIVertex vertex = source[j]; vertex.position += offset;
                vertex.color = new Color32(0, 0, 0, vertex.color.a);
                outlined.Add(vertex);
            }
        }
    }
}
