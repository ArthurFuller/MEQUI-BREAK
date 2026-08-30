using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Alterna os sprites ativos e inativos das abas sem assumir a seleção ou persistência.
/// Cabelo, roupa e acessório são apresentados visualmente como chapéu, rosto e cor.
/// </summary>
[DisallowMultipleComponent]
public sealed class CustomizationTabArtController : MonoBehaviour
{
    [SerializeField] private Button hatButton;
    [SerializeField] private Button faceButton;
    [SerializeField] private Button colorButton;
    [SerializeField] private Sprite hatActive;
    [SerializeField] private Sprite hatInactive;
    [SerializeField] private Sprite faceActive;
    [SerializeField] private Sprite faceInactive;
    [SerializeField] private Sprite colorActive;
    [SerializeField] private Sprite colorInactive;

    private void Awake()
    {
        ShowHat();
    }

    private void OnEnable()
    {
        hatButton?.onClick.AddListener(ShowHat);
        faceButton?.onClick.AddListener(ShowFace);
        colorButton?.onClick.AddListener(ShowColor);
    }

    private void OnDisable()
    {
        hatButton?.onClick.RemoveListener(ShowHat);
        faceButton?.onClick.RemoveListener(ShowFace);
        colorButton?.onClick.RemoveListener(ShowColor);
    }

    private void ShowHat() => Apply(hatActive, faceInactive, colorInactive);
    private void ShowFace() => Apply(hatInactive, faceActive, colorInactive);
    private void ShowColor() => Apply(hatInactive, faceInactive, colorActive);

    private void Apply(Sprite hat, Sprite face, Sprite color)
    {
        SetSprite(hatButton, hat);
        SetSprite(faceButton, face);
        SetSprite(colorButton, color);
    }

    private static void SetSprite(Button button, Sprite sprite)
    {
        if (button != null && button.targetGraphic is Image image)
            image.sprite = sprite;
    }
}
