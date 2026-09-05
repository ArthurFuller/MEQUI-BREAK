using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class AvatarView : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Abra este asset para acessar AJUSTES MANUAIS - CHAPÉUS.")]
    [FormerlySerializedAs("catalog")]
    [SerializeField] private AvatarCustomizationCatalog catalogoDeAjustes;

    [Header("Camadas do avatar")]
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image faceImage;
    [SerializeField] private Image hatImage;

    [Header("Opções")]
    [SerializeField] private Sprite bodySprite;
    [SerializeField] private Sprite[] faceOptions;
    [SerializeField] private Sprite[] hatOptions;
    [SerializeField] private Color[] colorOptions;

    [Header("Pré-visualização em tempo real")]
#if UNITY_EDITOR
    [Tooltip("Atualiza o avatar na Scene View sem precisar dar Play.")]
    [SerializeField] private bool atualizarNoEditor = true;
#endif
    [Tooltip("Quando ativo, selecione o objeto Hat e use as ferramentas Move, Scale e Rotate da Scene View. Os valores serão salvos no chapéu escolhido no catálogo.")]
    [SerializeField] private bool editarChapeuDiretamenteNaScene;
    [Range(0, 11)]
    [SerializeField] private int rostoNaPrevia;
    [Range(0, 11)]
    [SerializeField] private int corNaPrevia;

    [Header("Reações opcionais")]
    [SerializeField] private Animator animator;

#if UNITY_EDITOR
    private void OnEnable()
    {
        AgendarPreviaNoEditor();
    }

    private void OnValidate()
    {
        AgendarPreviaNoEditor();
    }

    private void OnDisable()
    {
        UnityEditor.EditorApplication.delayCall -= AtualizarPreviaNoEditor;
    }

    private void Update()
    {
        if (Application.isPlaying || !atualizarNoEditor || !editarChapeuDiretamenteNaScene)
            return;

        if (hatImage == null || catalogoDeAjustes == null)
            return;

        if (UnityEditor.Selection.activeGameObject != hatImage.gameObject)
            return;

        SalvarTransformacaoVisualDoChapeu();
    }

    private void AgendarPreviaNoEditor()
    {
        if (Application.isPlaying)
            return;

        UnityEditor.EditorApplication.delayCall -= AtualizarPreviaNoEditor;
        UnityEditor.EditorApplication.delayCall += AtualizarPreviaNoEditor;
    }

    public void AtualizarPreviaNoEditor()
    {
        UnityEditor.EditorApplication.delayCall -= AtualizarPreviaNoEditor;

        if (this == null)
            return;

        if (Application.isPlaying || !atualizarNoEditor)
            return;

        Apply(new AvatarCustomizationData
        {
            HatIndex = catalogoDeAjustes != null ? catalogoDeAjustes.ChapeuMostradoNaPrevia : 0,
            FaceIndex = rostoNaPrevia,
            ColorIndex = corNaPrevia
        });
    }

    public bool UsaCatalogo(AvatarCustomizationCatalog catalogo)
    {
        return catalogoDeAjustes == catalogo;
    }

    private void SalvarTransformacaoVisualDoChapeu()
    {
        int optionIndex = catalogoDeAjustes.ChapeuMostradoNaPrevia;
        AvatarCustomizationItem item = catalogoDeAjustes.GetItem(
            AvatarCustomizationCategory.Hat,
            optionIndex);
        RectTransform target = hatImage.rectTransform;

        if (item == null || target.rect.width <= 0f || target.rect.height <= 0f)
            return;

        Vector2 offset = new Vector2(
            target.anchoredPosition.x / target.rect.width,
            target.anchoredPosition.y / target.rect.height);
        float scale = Mathf.Max(0.1f, (Mathf.Abs(target.localScale.x) + Mathf.Abs(target.localScale.y)) * 0.5f);
        float rotation = target.localEulerAngles.z;
        if (rotation > 180f)
            rotation -= 360f;

        if (Vector2.SqrMagnitude(item.DeslocamentoNoAvatar - offset) < 0.0000001f
            && Mathf.Abs(item.EscalaNoAvatar - scale) < 0.0001f
            && Mathf.Abs(item.RotacaoNoAvatar - rotation) < 0.001f)
            return;

        item.DeslocamentoNoAvatar = offset;
        item.EscalaNoAvatar = scale;
        item.RotacaoNoAvatar = rotation;
        UnityEditor.EditorUtility.SetDirty(catalogoDeAjustes);
    }
#endif

    public void Apply(AvatarCustomizationData data)
    {
        if (data == null)
            return;

        ApplyBodyColor(data.ColorIndex);
        ApplyFace(data.FaceIndex);
        SetSprite(hatImage, hatOptions, data.HatIndex);
        ApplyItemTransform(hatImage, AvatarCustomizationCategory.Hat, data.HatIndex);
    }

    public void ApplyFace(int faceIndex)
    {
        SetSprite(faceImage, faceOptions, faceIndex);
        ApplyItemTransform(faceImage, AvatarCustomizationCategory.Face, faceIndex);
    }

    public void PlayReaction(string triggerName)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            animator.SetTrigger(triggerName);
    }

    private void ApplyBodyColor(int colorIndex)
    {
        if (bodyImage == null)
            return;

        if (bodySprite != null)
            bodyImage.sprite = bodySprite;

        bodyImage.enabled = bodyImage.sprite != null;
        bodyImage.color = GetColor(colorOptions, colorIndex, Color.white);
    }

    private static void SetSprite(Image target, Sprite[] options, int index)
    {
        if (target == null)
            return;

        Sprite selectedSprite = null;
        if (options != null && options.Length > 0)
        {
            int safeIndex = Mathf.Clamp(index, 0, options.Length - 1);
            selectedSprite = options[safeIndex];
        }

        target.sprite = selectedSprite;
        target.enabled = selectedSprite != null;
    }

    private static Color GetColor(Color[] options, int index, Color fallback)
    {
        if (options == null || options.Length == 0)
            return fallback;

        return options[Mathf.Clamp(index, 0, options.Length - 1)];
    }

    private void ApplyItemTransform(Image image, AvatarCustomizationCategory category, int optionIndex)
    {
        RectTransform target = image != null ? image.rectTransform : null;
        if (target == null)
            return;

        AvatarCustomizationItem item = catalogoDeAjustes != null
            ? catalogoDeAjustes.GetItem(category, optionIndex)
            : null;

        Vector2 offset = item != null ? item.DeslocamentoNoAvatar : Vector2.zero;
        float scale = item != null && item.EscalaNoAvatar > 0f ? item.EscalaNoAvatar : 1f;
        float rotation = item != null ? item.RotacaoNoAvatar : 0f;
        Rect rect = target.rect;

        target.anchoredPosition = new Vector2(offset.x * rect.width, offset.y * rect.height);
        target.localScale = new Vector3(scale, scale, 1f);
        target.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }
}
