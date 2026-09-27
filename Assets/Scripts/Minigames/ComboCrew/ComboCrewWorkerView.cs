using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Personagem montado na cena; a bancada deve estar em uma camada acima dele.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewWorkerView : MonoBehaviour
{
    [SerializeField] private RectTransform actor;
    [SerializeField] private GameObject carriedTray;
    [SerializeField] private GameObject[] carriedItems;
    [SerializeField] private AvatarView playerAvatar;
    [Header("Especialidade")]
    [SerializeField] private string displayName = "Equipe";
    [SerializeField, TextArea(2, 3)] private string description = "Desempenho equilibrado.";
    [SerializeField] private string shortTrait = "EQUILIBRADO";
    [SerializeField] private float[] stationSpeed = { 1f, 1f, 1f, 1f, 1f };
    [SerializeField, Range(.5f, 1.5f)] private float repairTimeMultiplier = 1f;
    [SerializeField, Min(0f)] private float pauseWeight = 1f;
    [Header("Pausa")]
    [SerializeField] private Image breakRing;
    [SerializeField] private GameObject breakBubble;
    [SerializeField] private TMP_Text breakBubbleText;
    [SerializeField] private Image face;
    [SerializeField] private Sprite problemFace;
    [SerializeField] private string pausePhrase = "Já volto!";
    [SerializeField] private RectTransform leftExit;
    [SerializeField] private RectTransform rightExit;
    [SerializeField, Min(.05f)] private float travelSeconds = .36f;
    [SerializeField, Min(0f)] private float handoffSeconds = .12f;
    [SerializeField, Min(0f)] private float deliveryWaitSeconds = 2f;
    private Sequence route;
    private RectTransform home;
    private CanvasGroup actorGroup;
    private float originalAlpha;
    private bool originalRaycasts;
    private Sprite savedFace;
    private bool problemShown;
    private const string ProblemPhrase = "Preciso de ajuda!";
    public bool Busy => route != null && route.IsActive();
    public bool Dragging => actor != null && actor.GetComponent<ComboCrewWorkerStationDrag>()?.IsDragging == true;
    public float RouteProgress => Busy ? route.ElapsedPercentage(false) : 0f;
    public bool Configured => actor != null && carriedTray != null;
    public string DisplayName => displayName;
    public string Description => description;
    public string ShortTrait => shortTrait;
    public float PauseWeight => pauseWeight;
    public float RepairTimeMultiplier => repairTimeMultiplier;
    public float SpeedAt(int station) => stationSpeed != null && station >= 0
        && station < stationSpeed.Length ? Mathf.Max(.1f, stationSpeed[station]) : 1f;

    public void ShowBreakWarning(float remainingFraction)
    {
        if (breakRing == null) return;
        breakRing.gameObject.SetActive(remainingFraction > 0f);
        breakRing.type = Image.Type.Filled;
        breakRing.fillMethod = Image.FillMethod.Radial360;
        breakRing.fillOrigin = (int)Image.Origin360.Top;
        breakRing.fillAmount = Mathf.Clamp01(remainingFraction);
    }

    public void ShowStationProblem(bool visible)
    {
        if (problemShown == visible) return;
        problemShown = visible;
        if (visible)
        {
            if (face != null && problemFace != null)
            {
                savedFace = face.sprite;
                face.sprite = problemFace;
            }
            if (breakBubble != null && !Busy)
            {
                if (breakBubbleText != null) breakBubbleText.text = ProblemPhrase;
                breakBubble.SetActive(true);
            }
        }
        else
        {
            if (face != null && savedFace != null) face.sprite = savedFace;
            savedFace = null;
            // A fala de pausa tem prioridade se o funcionário sair nesse instante.
            if (breakBubble != null && breakBubbleText != null
                && breakBubbleText.text == ProblemPhrase) breakBubble.SetActive(false);
        }
    }

    public bool TakeBreak(float seconds, Action returned)
    {
        if (home == null || Busy || leftExit == null || rightExit == null) return false;
        ShowStationProblem(false);
        ShowBreakWarning(0f);
        if (breakBubble != null) breakBubble.SetActive(true);
        if (breakBubbleText != null) breakBubbleText.text = pausePhrase;
        actor.DOKill();
        SetRaycasts(false);
        RectTransform exit = home.position.x < (leftExit.position.x + rightExit.position.x) * .5f
            ? leftExit : rightExit;
        Vector3 exitPosition = new Vector3(exit.position.x, actor.position.y, actor.position.z);
        route = DOTween.Sequence().SetTarget(this)
            .AppendInterval(.8f)
            .AppendCallback(() => { if (breakBubble != null) breakBubble.SetActive(false); })
            .Append(actor.DOMove(exitPosition, 1.1f).SetEase(Ease.InOutSine))
            .AppendInterval(seconds)
            .OnComplete(() =>
            {
                // A estação pode ter mudado enquanto o funcionário estava fora.
                route = DOTween.Sequence().SetTarget(this)
                    .Append(actor.DOMove(home.position, 1.1f).SetEase(Ease.InOutSine))
                    .OnComplete(() => { route = null; SetRaycasts(true); returned?.Invoke(); });
            });
        return true;
    }

    private void SetRaycasts(bool enabled)
    {
        if (actorGroup == null) actorGroup = actor.GetComponent<CanvasGroup>();
        if (actorGroup == null) return;
        if (!enabled) { originalAlpha = actorGroup.alpha; originalRaycasts = actorGroup.blocksRaycasts; }
        actorGroup.blocksRaycasts = enabled ? originalRaycasts : false;
    }

    public void SetTrayItems(int mask)
    {
        if (carriedItems == null) return;
        float totalWidth = 0f;
        int itemCount = 0;
        for (int i = 0; i < carriedItems.Length; i++)
        {
            if (carriedItems[i] == null) continue;
            bool visible = (mask & (1 << i)) != 0;
            carriedItems[i].SetActive(visible);
            if (visible && carriedItems[i].transform is RectTransform item)
            {
                totalWidth += item.rect.width;
                itemCount++;
            }
        }

        // Agrupa somente os itens do pedido, sem reservar espaço para os ausentes.
        const float overlap = 6f; // Compensa a margem transparente dentro dos PNGs.
        totalWidth -= Mathf.Max(0, itemCount - 1) * overlap;
        float left = -totalWidth * .5f;
        for (int i = 0; i < carriedItems.Length; i++)
        {
            if (carriedItems[i] == null || !carriedItems[i].activeSelf
                || !(carriedItems[i].transform is RectTransform item)) continue;
            float width = item.rect.width;
            item.anchoredPosition = new Vector2(left + width * .5f, item.anchoredPosition.y);
            left += width - overlap;
        }
    }

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
        SetTrayItems(0);
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
            .AppendCallback(() =>
            {
                carriedTray.SetActive(false);
                delivered?.Invoke();
                SetTrayItems(0); // Retorna com a bandeja suja, agora vazia.
            })
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
        SetTrayItems(0);
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
        ShowStationProblem(false);
        if (actorGroup != null) SetAbsent(false);
        route?.Kill();
        route = null;
        actor?.DOKill();
        if (carriedTray != null) carriedTray.SetActive(false);
        if (breakBubble != null) breakBubble.SetActive(false);
        ShowBreakWarning(0f);
        SetTrayItems(0);
        if (actorGroup != null) actorGroup.blocksRaycasts = originalRaycasts;
        if (actor != null && home != null) actor.position = home.position;
    }
}
