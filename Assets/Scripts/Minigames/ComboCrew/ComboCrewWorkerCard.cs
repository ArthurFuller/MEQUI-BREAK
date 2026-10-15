using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>Card fixo na prancheta, com rosto e duas funções configurados na Hierarchy.</summary>
public sealed class ComboCrewWorkerCard : HierarchyDragHandle, IPointerClickHandler
{
    [SerializeField] private ComboCrewController owner;
    [SerializeField, Min(0)] private int workerIndex;
    [SerializeField] private GameObject portraitWhileDragging;
    [SerializeField] private GameObject cardDetails;
    [SerializeField] private ComboCrewClipboard clipboard;
    [SerializeField] private TMP_Text traitLabel;
    [SerializeField, Min(.1f)] private float idleNudgeDelay = 7f;
    private float idleTime;
    private Tween idleNudge;
    private Quaternion nudgeOriginRotation;
    public int WorkerIndex => workerIndex;
    public bool Configured => DragConfigured && owner != null && clipboard != null
        && portraitWhileDragging != null && cardDetails != null;
    protected override bool CanDrag => owner != null && owner.CanMoveWorker(workerIndex);
    private void Start()
    {
        if (traitLabel != null && owner != null)
            traitLabel.text = owner.WorkerTrait(workerIndex);
    }

    private void Update()
    {
        if (owner == null || !owner.CanNudgeWorkerCards || IsDragging)
        {
            idleTime = 0f;
            StopNudge();
            return;
        }

        idleTime += Time.unscaledDeltaTime;
        if (idleTime < idleNudgeDelay + workerIndex * 1.2f || !(transform is RectTransform rect)) return;
        idleTime = 0f;
        StopNudge();
        nudgeOriginRotation = rect.localRotation;
        float angle = nudgeOriginRotation.eulerAngles.z;
        idleNudge = DOTween.Sequence().SetTarget(this).SetUpdate(true)
            .Append(rect.DOLocalRotate(new Vector3(0f, 0f, angle + 5f), .28f).SetEase(Ease.InOutSine))
            .Append(rect.DOLocalRotate(new Vector3(0f, 0f, angle - 5f), .42f).SetEase(Ease.InOutSine))
            .Append(rect.DOLocalRotate(new Vector3(0f, 0f, angle), .3f).SetEase(Ease.OutSine));
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && !IsDragging
            && Time.unscaledTime - LastDragEndTime > .2f)
            owner?.OpenProfile(workerIndex);
    }
    protected override Vector3 RestPosition => owner != null && owner.CardHome(workerIndex) != null
        ? owner.CardHome(workerIndex).position : transform.position;

    protected override void DragStateChanged(bool dragging)
    {
        StopNudge();
        if (portraitWhileDragging != null) portraitWhileDragging.SetActive(dragging);
        if (cardDetails != null) cardDetails.SetActive(!dragging);
        if (dragging)
        {
            transform.SetAsLastSibling();
            clipboard.CloseForDrag(DragVisual);
        }
    }

    protected override void HighlightTargets(bool visible) => owner?.HighlightStations(visible, workerIndex);
    protected override void DragMoved() => clipboard?.TrackDraggedCard(DragVisual);
    protected override void DragFinished(bool accepted)
    {
        if (!accepted) { AudioManager.Instance?.PlayError(); MequiHaptics.Reject(); }
        // O card volta à sua âncora local antes de a prancheta subir.
        // Assim não há tween para a posição fora da tela nem salto ao final.
        MoveHome(true);
        clipboard.OpenAfterDrag();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopNudge();
        DOTween.Kill(this);
    }

    private void StopNudge()
    {
        if (idleNudge == null) return;
        bool active = idleNudge.IsActive();
        idleNudge.Kill();
        if (active) transform.localRotation = nudgeOriginRotation;
        idleNudge = null;
    }
}
