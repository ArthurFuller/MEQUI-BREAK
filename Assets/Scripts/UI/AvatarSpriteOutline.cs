using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>Contorno uniforme em uma única malha, com material compartilhado e suporte a máscaras.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Graphic))]
public sealed class AvatarSpriteOutline : BaseMeshEffect, IMaterialModifier
{
    [SerializeField, Min(0f), Tooltip("Espessura em unidades do Canvas, independente da escala do avatar.")]
    private float thickness = 10f;
    private static Material template;
    private Material maskedMaterial;
    private Vector3 axisX = Vector3.right, axisY = Vector3.up;
    private readonly UIVertex[] quad = new UIVertex[4];
    private static bool missingMaterialReported;

    public float Thickness
    {
        get => thickness;
        set
        {
            float next = Mathf.Max(0f, value);
            if (Mathf.Approximately(thickness, next)) return;
            thickness = next;
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    private void CanvasAxes(out Vector3 x, out Vector3 y)
    {
        Canvas canvas = graphic != null ? graphic.canvas : null;
        Transform reference = canvas != null ? canvas.rootCanvas.transform : null;
        x = transform.InverseTransformVector(reference != null ? reference.TransformVector(Vector3.right) : Vector3.right);
        y = transform.InverseTransformVector(reference != null ? reference.TransformVector(Vector3.up) : Vector3.up);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (graphic != null) graphic.SetMaterialDirty();
    }

    protected override void OnDisable()
    {
        ReleaseMaterial();
        base.OnDisable();
        if (graphic != null) graphic.SetMaterialDirty();
    }

    private void ReleaseMaterial()
    {
        if (maskedMaterial != null) StencilMaterial.Remove(maskedMaterial);
        maskedMaterial = null;
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!IsActive()) { ReleaseMaterial(); return baseMaterial; }
        if (template == null) template = Resources.Load<Material>("UI/AvatarOutline");
        if (template == null)
        {
            if (!missingMaterialReported)
            {
                missingMaterialReported = true;
                Debug.LogError("Material UI/AvatarOutline ausente. Confira Assets/Resources/UI.", this);
            }
            return baseMaterial;
        }
        if (graphic.canvas != null)
            graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
        // StencilMaterial mantém cache e contagem de referências: avatares com
        // a mesma máscara compartilham material e continuam agrupáveis.
        Material next = StencilMaterial.Add(template,
            baseMaterial.GetInt("_Stencil"),
            (StencilOp)baseMaterial.GetInt("_StencilOp"),
            (CompareFunction)baseMaterial.GetInt("_StencilComp"),
            (ColorWriteMask)baseMaterial.GetInt("_ColorMask"),
            baseMaterial.GetInt("_StencilReadMask"), baseMaterial.GetInt("_StencilWriteMask"));
        ReleaseMaterial();
        maskedMaterial = next;
        return next;
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
        if (!IsActive() || vertices.currentVertCount != 4) return;
        for (int i = 0; i < 4; i++) vertices.PopulateUIVertex(ref quad[i], i);
        Vector2 min = quad[0].position, max = min;
        Vector2 uvMin = quad[0].uv0, uvMax = uvMin;
        for (int i = 1; i < 4; i++)
        {
            min = Vector2.Min(min, quad[i].position); max = Vector2.Max(max, quad[i].position);
            uvMin = Vector2.Min(uvMin, quad[i].uv0); uvMax = Vector2.Max(uvMax, quad[i].uv0);
        }
        Vector2 size = max - min;
        if (size.x <= .001f || size.y <= .001f) return;
        CanvasAxes(out axisX, out axisY);
        Vector2 uvPerUnit = new Vector2((uvMax.x - uvMin.x) / size.x, (uvMax.y - uvMin.y) / size.y);
        float radius = Mathf.Max(0f, thickness);
        Vector2 padding = radius * new Vector2(Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x), Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y));
        Vector4 steps = new Vector4(axisX.x * uvPerUnit.x, axisX.y * uvPerUnit.y,
            axisY.x * uvPerUnit.x, axisY.y * uvPerUnit.y) * radius;
        Vector4 bounds = new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y);
        for (int i = 0; i < 4; i++)
        {
            UIVertex vertex = quad[i];
            Vector2 direction = new Vector2(vertex.position.x < (min.x + max.x) * .5f ? -1f : 1f,
                vertex.position.y < (min.y + max.y) * .5f ? -1f : 1f);
            Vector2 expansion = Vector2.Scale(direction, padding);
            vertex.position += (Vector3)expansion;
            vertex.uv0 += new Vector4(expansion.x * uvPerUnit.x, expansion.y * uvPerUnit.y, 0f, 0f);
            vertex.uv1 = bounds; vertex.uv2 = steps;
            vertices.SetUIVertex(vertex, i);
        }
    }
}
