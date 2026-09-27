using System;
using DG.Tweening;
using UnityEngine;

/// <summary>Personagem montado na cena; a bancada deve estar em uma camada acima dele.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewWorkerView : MonoBehaviour
{
    [SerializeField] private RectTransform actor;
    [SerializeField] private GameObject carriedTray;
    [SerializeField] private AvatarView playerAvatar;
    [SerializeField, Min(.05f)] private float travelSeconds = .36f;
    [SerializeField, Min(0f)] private float handoffSeconds = .12f;
    [SerializeField, Min(0f)] private float deliveryWaitSeconds = 2f;
    private Sequence route;
    private RectTransform home;
    private CanvasGroup actorGroup;
    private float originalAlpha;
    private bool originalRaycasts;
    public bool Busy => route != null && route.IsActive();
    public bool Dragging => actor != null && actor.GetComponent<ComboCrewWorkerStationDrag>()?.IsDragging == true;
    public float RouteProgress => Busy ? route.ElapsedPercentage(false) : 0f;
    public bool Configured => actor != null && carriedTray != null;

    public void SetAbsent(bool value)
    {
        if (actorGroup == null)
        {
            actorGroup = actor.GetComponent<CanvasGroup>();
            if (actorGroup == null) return;
            originalAlpha = actorGroup.alpha;
            originalRaycasts = actorGroup.blocksRaycasts;
        }
        actorGroup.alpha = value ? .35f : originalAlpha;
        actorGroup.blocksRaycasts = value ? false : originalRaycasts;
    }

    public void ApplyPlayerAppearance()
    {
        if (playerAvatar != null && PlayerManager.Instance?.Profile?.Avatar != null)
            playerAvatar.Apply(PlayerManager.Instance.Profile.Avatar);
    }

    public void Place(RectTransform anchor, bool instant)
    {
        if (anchor == null || actor == null) return;
        home = anchor;
        if (Busy) return;
        if (!actor.gameObject.activeSelf) actor.gameObject.SetActive(true);
        actor.DOKill();
        if (instant) actor.position = anchor.position;
        else actor.DOMove(anchor.position, travelSeconds).SetEase(Ease.OutCubic).SetTarget(this);
    }

    public void Travel(RectTransform destination, Action handedOff, Action completed)
    {
        if (!Configured || home == null || destination == null || Busy) return;
        carriedTray.SetActive(true);
        route = DOTween.Sequence().SetTarget(this)
            .Append(actor.DOMove(destination.position, travelSeconds).SetEase(Ease.InOutCubic))
            .AppendInterval(handoffSeconds)
            .AppendCallback(() => carriedTray.SetActive(false))
            .AppendCallback(() => handedOff?.Invoke())
            .Append(actor.DOMove(home.position, travelSeconds).SetEase(Ease.InOutCubic))
            .OnComplete(() => { route = null; completed?.Invoke(); });
    }

    public void Deliver(RectTransform destination, Action delivered, Action returned)
    {
        if (!Configured || home == null || destination == null || Busy) return;
        carriedTray.SetActive(true);
        route = DOTween.Sequence().SetTarget(this)
            .Append(actor.DOMove(destination.position, travelSeconds * 1.6f).SetEase(Ease.InOutCubic))
            .AppendCallback(() => carriedTray.SetActive(false))
            .AppendCallback(() => delivered?.Invoke())
            .AppendInterval(deliveryWaitSeconds)
            .AppendCallback(() => carriedTray.SetActive(true))
            .Append(actor.DOMove(home.position, travelSeconds * 1.6f).SetEase(Ease.InOutCubic))
            .AppendCallback(() => carriedTray.SetActive(false))
            .OnComplete(() => { route = null; returned?.Invoke(); });
    }

    public void Wash(RectTransform counter, RectTransform firstStation, int count,
        float secondsPerTray, Action collected, Action completed)
    {
        if (!Configured || home == null || counter == null || firstStation == null || Busy || count < 1) return;
        route = DOTween.Sequence().SetTarget(this)
            .Append(actor.DOMove(counter.position, travelSeconds + count * .08f).SetEase(Ease.InOutCubic))
            .AppendCallback(() => carriedTray.SetActive(true))
            .AppendCallback(() => collected?.Invoke())
            .Append(actor.DOMove(home.position, travelSeconds + count * .08f).SetEase(Ease.InOutCubic))
            .AppendInterval(secondsPerTray * count)
            .Append(actor.DOMove(firstStation.position, travelSeconds + count * .08f).SetEase(Ease.InOutCubic))
            .AppendCallback(() => carriedTray.SetActive(false))
            .Append(actor.DOMove(home.position, travelSeconds).SetEase(Ease.InOutCubic))
            .OnComplete(() => { route = null; completed?.Invoke(); });
    }

    public void SetPaused(bool value)
    {
        if (!Busy) return;
        if (value) route.Pause();
        else route.Play();
    }

    private void OnDisable()
    {
        if (actorGroup != null) SetAbsent(false);
        route?.Kill();
        route = null;
        actor?.DOKill();
        if (carriedTray != null) carriedTray.SetActive(false);
        if (actor != null && home != null) actor.position = home.position;
    }
}
