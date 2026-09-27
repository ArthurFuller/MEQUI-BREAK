using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Permite mudar um personagem já colocado sem abrir a prancheta.</summary>
public sealed class ComboCrewWorkerStationDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private ComboCrewController owner;
    [SerializeField, Min(0)] private int workerIndex;
    [SerializeField] private RectTransform actor;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private CanvasGroup canvasGroup;

    private Vector3 origin;
    private Vector3 offset;
    private int pointerId;
    private bool dragging, accepted;
    public bool IsDragging => dragging;
    public int WorkerIndex => workerIndex;
    public bool OwnsPointer(PointerEventData data) => dragging && pointerId == data.pointerId;

    public void OnBeginDrag(PointerEventData data)
    {
        if (dragging || data.button != PointerEventData.InputButton.Left || owner == null
            || !owner.CanMoveWorker(workerIndex) || actor == null || canvasRect == null
            || canvasGroup == null || !Point(data, out Vector3 position)) return;
        actor.DOKill();
        origin = actor.position;
        offset = origin - position;
        pointerId = data.pointerId;
        dragging = true;
        accepted = false;
        actor.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
        owner.HighlightStations(true);
    }

    public void OnDrag(PointerEventData data)
    {
        if (OwnsPointer(data) && Point(data, out Vector3 position)) actor.position = position + offset;
    }

    public void AcceptCurrentDrop() { if (dragging) accepted = true; }

    public void OnEndDrag(PointerEventData data)
    {
        if (!OwnsPointer(data)) return;
        dragging = false;
        canvasGroup.blocksRaycasts = true;
        owner.HighlightStations(false);
        if (!accepted) actor.DOMove(origin, .16f).SetEase(Ease.OutCubic);
    }

    private bool Point(PointerEventData data, out Vector3 position) =>
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect, data.position, data.pressEventCamera, out position);

    private void OnDisable()
    {
        dragging = false;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
    }
}
