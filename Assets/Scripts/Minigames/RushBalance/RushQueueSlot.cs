using UnityEngine;
using UnityEngine.EventSystems;

public sealed class RushQueueSlot : MonoBehaviour, IDropHandler
{
    [SerializeField] private RushBalanceController controller;
    [SerializeField, Min(0)] private int rank;
    [SerializeField] private GameObject highlight;
    public bool ConfiguredFor(RushBalanceController owner, int index) => controller == owner && rank == index && highlight != null;
    public void Highlight(bool value) { if (highlight != null) highlight.SetActive(value); }
    public void OnDrop(PointerEventData data)
    {
        if (controller == null || data.pointerDrag == null) return;
        RushOrderView order = data.pointerDrag.GetComponent<RushOrderView>();
        if (order != null && order.Owner == controller && order.OwnsPointer(data))
            controller.Reorder(order.OrderId, rank);
    }
}
