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
    private Transform homeParent;
    private int homeSibling;
    private CanvasGroup actorGroup;
    private bool actorRaycasts;
    private int pointerId;
    private bool dragging, accepted;
    public bool IsDragging => dragging;
    public int WorkerIndex => workerIndex;
    public bool OwnsPointer(PointerEventData data) => dragging && pointerId == data.pointerId;

    private void Awake()
    {
        if (actor != null)
            actorGroup = actor.GetComponent<CanvasGroup>() ?? actor.gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (dragging || data.button != PointerEventData.InputButton.Left || owner == null
            || !owner.CanMoveWorker(workerIndex) || actor == null || canvasRect == null
            || canvasGroup == null || actorGroup == null || !Point(data, out Vector3 position)) return;
        actor.DOKill();
        origin = actor.position;
        offset = origin - position;
        homeParent = actor.parent;
        homeSibling = actor.GetSiblingIndex();
        pointerId = data.pointerId;
        dragging = true;
        accepted = false;
        actor.SetParent(canvasRect, true);
        actor.SetAsLastSibling();
        actorRaycasts = actorGroup.blocksRaycasts;
        actorGroup.blocksRaycasts = false;
        canvasGroup.blocksRaycasts = false;
        owner.HighlightStations(true, workerIndex);
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
        if (actorGroup != null) actorGroup.blocksRaycasts = actorRaycasts;
        canvasGroup.blocksRaycasts = true;
        owner.HighlightStations(false);
        if (!accepted) actor.DOMove(origin, .16f).SetEase(Ease.OutCubic);
        RestoreParent();
        if (accepted) owner.SnapWorkerToStation(workerIndex);
    }

    private bool Point(PointerEventData data, out Vector3 position) =>
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect, data.position, data.pressEventCamera, out position);

    private void RestoreParent()
    {
        if (actor == null || homeParent == null) return;
        actor.SetParent(homeParent, true);
        if (homeParent.childCount > 0)
            actor.SetSiblingIndex(Mathf.Clamp(homeSibling, 0, homeParent.childCount - 1));
        homeParent = null;
    }

    private void OnDisable()
    {
        dragging = false;
        if (actorGroup != null) actorGroup.blocksRaycasts = actorRaycasts;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        RestoreParent();
    }
}
