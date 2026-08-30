using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Alterna o ícone entre os estados normal e destacado durante o arraste.</summary>
[DisallowMultipleComponent]
public sealed class MequiDraggableArtState : MonoBehaviour,
    IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite highlightedSprite;

    // Mantém compatibilidade com a versão legada do sprite sem duplicá-lo na interface.
    [SerializeField] private Sprite legacyCompatibilitySprite;

    private void Awake() => Apply(normalSprite);

    public void OnBeginDrag(PointerEventData eventData)
    {
        Apply(highlightedSprite != null ? highlightedSprite : normalSprite);
    }

    public void OnEndDrag(PointerEventData eventData) => Apply(normalSprite);

    private void Apply(Sprite sprite)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.preserveAspect = true;
    }
}
