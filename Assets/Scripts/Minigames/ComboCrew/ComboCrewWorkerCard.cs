using UnityEngine;

/// <summary>Card fixo na prancheta, com rosto e duas funções configurados na Hierarchy.</summary>
public sealed class ComboCrewWorkerCard : HierarchyDragHandle
{
    [SerializeField] private ComboCrewController owner;
    [SerializeField, Min(0)] private int workerIndex;
    [SerializeField] private GameObject portraitWhileDragging;
    [SerializeField] private GameObject cardDetails;
    [SerializeField] private ComboCrewClipboard clipboard;
    public int WorkerIndex => workerIndex;
    public bool Configured => DragConfigured && owner != null && clipboard != null
        && portraitWhileDragging != null && cardDetails != null;
    protected override bool CanDrag => owner != null && owner.CanMoveWorker(workerIndex);
    protected override Vector3 RestPosition => owner != null && owner.CardHome(workerIndex) != null
        ? owner.CardHome(workerIndex).position : transform.position;

    protected override void DragStateChanged(bool dragging)
    {
        if (portraitWhileDragging != null) portraitWhileDragging.SetActive(dragging);
        if (cardDetails != null) cardDetails.SetActive(!dragging);
        if (dragging)
        {
            transform.SetAsLastSibling();
            clipboard.CloseForDrag(DragVisual);
        }
    }

    protected override void HighlightTargets(bool visible) => owner?.HighlightStations(visible);
    protected override void DragMoved() => clipboard?.TrackDraggedCard(DragVisual);
    protected override void DragFinished(bool accepted)
    {
        if (!accepted) { AudioManager.Instance?.PlayError(); MequiHaptics.Reject(); }
        // O card volta à sua âncora local antes de a prancheta subir.
        // Assim não há tween para a posição fora da tela nem salto ao final.
        MoveHome(true);
        clipboard.OpenAfterDrag();
    }
}
