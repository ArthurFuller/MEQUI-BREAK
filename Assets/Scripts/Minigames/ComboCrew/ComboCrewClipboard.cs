using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Botão e prancheta são filhos do mesmo RectTransform móvel.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewClipboard : MonoBehaviour
{
    [SerializeField] private RectTransform movingRoot;
    [SerializeField] private Button handleButton;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Vector2 closedPosition;
    [SerializeField] private Vector2 openPosition;
    [SerializeField, Min(.05f)] private float duration = .22f;
    private Tween movement;
    private bool open;
    private RectTransform draggedCard;
    private Vector3 draggedCardPosition;
    public bool Configured => movingRoot != null && handleButton != null && panelGroup != null;
    public bool IsOpen => open;

    private void Awake()
    {
        if (!Configured) return;
        movingRoot.anchoredPosition = closedPosition;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
        handleButton.onClick.AddListener(Toggle);
    }

    public void Toggle()
    {
        if (!Configured || draggedCard != null) return;
        SetOpen(!open);
    }

    public void CloseForDrag(RectTransform card)
    {
        if (!Configured || card == null) return;
        draggedCard = card;
        draggedCardPosition = card.position;
        SetOpen(false);
    }

    public void TrackDraggedCard(RectTransform card)
    {
        if (card != null && card == draggedCard) draggedCardPosition = card.position;
    }

    public void OpenAfterDrag()
    {
        if (!Configured) return;
        draggedCard = null;
        SetOpen(true);
        handleButton.interactable = false;
        movement.OnComplete(() =>
        {
            handleButton.interactable = true;
        });
    }

    private void SetOpen(bool value)
    {
        open = value;
        // Kill sem completar: a nova animação começa exatamente da posição atual.
        movement?.Kill();
        if (!open) handleButton.interactable = true;
        panelGroup.interactable = open;
        panelGroup.blocksRaycasts = open;
        Vector2 target = open ? openPosition : closedPosition;
        movement = movingRoot.DOAnchorPos(target, duration)
            .SetEase(open ? Ease.OutCubic : Ease.InOutCubic).SetTarget(this)
            .OnUpdate(() =>
            {
                // A prancheta anima sob o card, que permanece junto ao dedo.
                if (draggedCard != null) draggedCard.position = draggedCardPosition;
            });
        AudioManager.Instance?.PlayClick();
    }

    private void OnDestroy()
    {
        movement?.Kill();
        if (handleButton != null) handleButton.onClick.RemoveListener(Toggle);
    }
}
