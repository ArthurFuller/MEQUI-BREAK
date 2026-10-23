using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Personagem montado na cena; a bancada deve estar em uma camada acima dele.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewWorkerView : MonoBehaviour
{
    [SerializeField] private RectTransform actor;
    [SerializeField, Tooltip("Mova BandejaCarregada dentro do ator para ajustar onde ela aparece durante o transporte.")]
    private GameObject carriedTray;
    [SerializeField] private GameObject[] carriedItems;
    [SerializeField] private AvatarView playerAvatar;
    [Header("Especialidade")]
    [SerializeField] private string displayName = "Equipe";
    [SerializeField, Tooltip("Ordem: Preparo, Chapa, Montagem, Entrega, Lava-louça. 1 = normal; 1,2 = 20% mais velocidade.")]
    private float[] stationSpeed = { 1f, 1f, 1f, 1f, 1f };
    [SerializeField, Range(.5f, 1.5f), Tooltip("Multiplica o tempo de reparo. 0,7 = 30% menos tempo.")]
    private float repairTimeMultiplier = 1f;
    [SerializeField, Min(0f), Tooltip("Peso relativo para ser escolhido para uma pausa; 2 = peso duas vezes maior, não chance fixa.")]
    private float pauseWeight = 1f;
    [SerializeField, Range(.5f, 1.5f), Tooltip("Velocidade ao levar pedidos e voltar. 1,1 = 10% mais velocidade.")]
    private float transportSpeedMultiplier = 1f;
    [SerializeField, Range(.5f, 1.5f), Tooltip("Multiplica a duração da pausa. 1,25 = pausa 25% mais longa.")]
    private float breakDurationMultiplier = 1f;
    [Header("Pausa")]
    [SerializeField] private Image breakRing;
    [SerializeField] private Animator breakWarningAnimator;
    [SerializeField, Range(0f, 1f)] private float breakAlertFraction = .3f;
    private static readonly int ClockProgressId = Animator.StringToHash("ClockProgress");
    private static readonly int ClockUrgentId = Animator.StringToHash("Urgent");
    [SerializeField] private GameObject breakBubble;
    [SerializeField] private TMP_Text breakBubbleText;
    [SerializeField, Tooltip("Balão à direita do ator. Mova este objeto diretamente na cena.")]
    private GameObject breakBubbleRight;
    [SerializeField] private TMP_Text breakBubbleRightText;
    [SerializeField] private Sprite bubblePointingLeft;
    [SerializeField] private Sprite bubblePointingRight;
    [SerializeField, Tooltip("Camada à frente das bancadas usada durante o aviso e a pausa.")]
    private Transform pauseForegroundLayer;
    [SerializeField] private Image face;
    [SerializeField] private Sprite problemFace;
    [SerializeField] private string pausePhrase = "Já volto!";
    [SerializeField] private RectTransform leftExit;
    [SerializeField] private RectTransform rightExit;
    [SerializeField, Min(.05f)] private float travelSeconds = .36f;
    [SerializeField, Min(0f)] private float handoffSeconds = .12f;
    [SerializeField, Min(0f)] private float deliveryWaitSeconds = 1f;
    private Sequence route;
    private RectTransform home;
    private CanvasGroup actorGroup;
    private float originalAlpha;
    private bool originalRaycasts;
    private Sprite savedFace;
    private bool problemShown;
    private bool flowBlocked;
    private TMP_Text attentionMark;
    private Tween attentionPulse;
    private Tween bubbleTween;
    private bool bubbleOnRight;
    private GameObject ActiveBubble => bubbleOnRight && breakBubbleRight != null
        ? breakBubbleRight : breakBubble;
    private TMP_Text ActiveBubbleText => bubbleOnRight && breakBubbleRightText != null
        ? breakBubbleRightText : breakBubbleText;
    private Vector2 attentionHome;
    private Vector3 attentionActorScale;
    private bool attentionScaleCached;
    private Transform deliveryHomeParent;
    private int deliveryHomeSibling;
    private bool restoreLayerWhenActive;
    private const string ProblemPhrase = "Preciso de ajuda!";
    public bool Busy => route != null && route.IsActive();
    public bool WashingAtSink { get; private set; }
    public float WashProgress { get; private set; }
    public bool Dragging => actor != null && actor.GetComponent<ComboCrewWorkerStationDrag>()?.IsDragging == true;
    public float RouteProgress => Busy ? route.ElapsedPercentage(false) : 0f;
    public bool Configured => actor != null && carriedTray != null;
    public string DisplayName => displayName;
#if UNITY_EDITOR
    public RectTransform PreviewActor => actor;
#endif
    public void ApplyProfile(ComboCrewWorkerProfile profile)
    {
        if (profile == null) return;
        displayName = profile.displayName;
        if (stationSpeed == null || stationSpeed.Length != 5) stationSpeed = new float[5];
        for (int i = 0; i < 5; i++)
            stationSpeed[i] = profile.stationSpeed != null && i < profile.stationSpeed.Length
                ? Mathf.Clamp(profile.stationSpeed[i], .8f, 1.25f) : 1f;
        repairTimeMultiplier = profile.repairTimeMultiplier;
        pauseWeight = profile.pauseWeight;
        transportSpeedMultiplier = profile.transportSpeedMultiplier;
        breakDurationMultiplier = profile.breakDurationMultiplier;
        Image body = actor.Find("Corpo")?.GetComponent<Image>();
        Image hat = actor.Find("Chapeu")?.GetComponent<Image>();
        if (body != null && profile.head != null)
        {
            body.sprite = profile.head;
            body.color = Color.white;
        }
        if (face != null && profile.face != null) face.sprite = profile.face;
        if (hat != null && profile.hat != null)
        {
            hat.sprite = profile.hat;
            hat.gameObject.SetActive(true);
        }
    }
    public void CopyPortrait(Image body, Image portraitFace, Image hat)
    {
        if (actor == null) return;
        Image actorBody = actor.Find("Corpo")?.GetComponent<Image>();
        Image actorHat = actor.Find("Chapeu")?.GetComponent<Image>();
        if (body != null && actorBody != null)
        {
            body.sprite = actorBody.sprite;
            body.color = actorBody.color;
            body.preserveAspect = true;
            CopyPortraitOutline(actorBody, body);
        }
        if (portraitFace != null && face != null)
        {
            portraitFace.sprite = face.sprite;
            portraitFace.color = face.color;
        }
        if (hat != null)
        {
            bool visible = actorHat != null && actorHat.gameObject.activeSelf && actorHat.sprite != null;
            hat.gameObject.SetActive(visible);
            if (visible)
            {
                hat.sprite = actorHat.sprite;
                hat.color = actorHat.color;
                CopyPortraitOutline(actorHat, hat);
            }
        }
    }
    private static void CopyPortraitOutline(Image source, Image target)
    {
        AvatarSpriteOutline outline = target.GetComponent<AvatarSpriteOutline>();
        if (outline != null)
            outline.enabled = source.GetComponent<AvatarSpriteOutline>()?.isActiveAndEnabled == true;
    }
    public float PauseWeight => pauseWeight;
    public float RepairTimeMultiplier => repairTimeMultiplier;
    public float BreakDurationMultiplier => breakDurationMultiplier > 0f ? breakDurationMultiplier : 1f;
    private float TransportSpeed => transportSpeedMultiplier > 0f ? transportSpeedMultiplier : 1f;
    public float SpeedAt(int station) => stationSpeed != null && station >= 0
        && station < stationSpeed.Length ? Mathf.Max(.1f, stationSpeed[station]) : 1f;
    public float CycleSeconds(int station, float workSeconds)
    {
        float routeSeconds = 2f * travelSeconds / TransportSpeed;
        return workSeconds / SpeedAt(station) + .18f + routeSeconds
            + (station == 3 ? deliveryWaitSeconds : handoffSeconds);
    }

    private readonly struct Trait
    {
        public readonly string Label, Detail;
        public readonly bool Positive;
        public readonly float Strength;
        public Trait(string label, string detail, bool positive, float strength)
        { Label = label; Detail = detail; Positive = positive; Strength = strength; }
    }

    public readonly struct CardBadge
    {
        public readonly string Label;
        public readonly int Tone;
        public CardBadge(string label, int tone) { Label = label; Tone = tone; }
    }

    private static readonly string[] StationLabels =
        { "PREPARO", "CHAPA", "MONTAGEM", "ENTREGA", "LAVA-LOUÇA" };
    private const string GoodColor = "#79D6A2";
    private const string BadColor = "#FF8977";
    private const string NeutralColor = "#F8C94B";

    private List<Trait> GetTraits()
    {
        List<Trait> traits = new List<Trait>(9);
        for (int i = 0; i < StationLabels.Length; i++)
        {
            float speed = SpeedAt(i);
            int change = Mathf.RoundToInt((speed - 1f) * 100f);
            if (change == 0) continue;
            string task = i == 4 ? "lavagem" : "trabalho";
            traits.Add(new Trait(StationLabels[i],
                $"{StationLabels[i]}: velocidade de {task} {change:+0;-0}%",
                change > 0, Mathf.Abs(change)));
        }
        int transport = Mathf.RoundToInt((TransportSpeed - 1f) * 100f);
        if (transport != 0)
            traits.Add(new Trait("TRANSPORTE",
                $"Transporte de pedidos: velocidade {transport:+0;-0}%",
                transport > 0, Mathf.Abs(transport)));
        int repair = Mathf.RoundToInt((repairTimeMultiplier - 1f) * 100f);
        if (repair != 0)
            traits.Add(new Trait(repair < 0 ? "REPARO RÁPIDO" : "REPARO LENTO",
                $"Manutenção na bancada ocupada: tempo de reparo {repair:+0;-0}%",
                repair < 0, Mathf.Abs(repair)));
        int breakTime = Mathf.RoundToInt((BreakDurationMultiplier - 1f) * 100f);
        if (breakTime != 0)
            traits.Add(new Trait(breakTime < 0 ? "PAUSA CURTA" : "PAUSA LONGA",
                $"Pausas: duração {breakTime:+0;-0}%",
                breakTime < 0, Mathf.Abs(breakTime)));
        int pauseSelection = Mathf.RoundToInt((pauseWeight - 1f) * 100f);
        if (pauseSelection != 0)
            traits.Add(new Trait(pauseSelection < 0 ? "PAUSA RARA" : "PAUSA FREQUENTE",
                $"Pausas: peso na seleção {pauseSelection:+0;-0}% (chance depende da equipe)",
                pauseSelection < 0, Mathf.Abs(pauseSelection)));
        return traits;
    }

    public CardBadge[] CardBadges()
    {
        List<Trait> traits = GetTraits();
        if (traits.Count == 0) return new[] { new CardBadge("EQUILIBRADO", 0) };
        Trait? bestPositive = null, bestNegative = null;
        foreach (Trait trait in traits)
        {
            if (trait.Positive && (!bestPositive.HasValue || trait.Strength > bestPositive.Value.Strength))
                bestPositive = trait;
            if (!trait.Positive && (!bestNegative.HasValue || trait.Strength > bestNegative.Value.Strength))
                bestNegative = trait;
        }
        Trait first = bestPositive ?? bestNegative.Value;
        Trait? second = bestPositive.HasValue && bestNegative.HasValue ? bestNegative
            : BestOther(traits, first);
        CardBadge firstBadge = new CardBadge(first.Label, first.Positive ? 1 : -1);
        return second.HasValue
            ? new[] { firstBadge, new CardBadge(second.Value.Label, second.Value.Positive ? 1 : -1) }
            : new[] { firstBadge };
    }

    public string CardSummary()
    {
        CardBadge[] badges = CardBadges();
        string result = ColorBadge(badges[0]);
        if (badges.Length > 1) result += "\n" + ColorBadge(badges[1]);
        return result;
    }

    private static Trait? BestOther(List<Trait> traits, Trait first)
    {
        Trait? result = null;
        foreach (Trait trait in traits)
            if (trait.Label != first.Label && (!result.HasValue || trait.Strength > result.Value.Strength))
                result = trait;
        return result;
    }

    private static string ColorBadge(CardBadge badge) =>
        $"<color={(badge.Tone > 0 ? GoodColor : badge.Tone < 0 ? BadColor : NeutralColor)}>{(badge.Tone > 0 ? "+ " : badge.Tone < 0 ? "- " : "")}{badge.Label}</color>";

    public string ProfileDetails()
    {
        List<Trait> traits = GetTraits();
        if (traits.Count == 0) return "Ritmo padrão em todas as funções.\nSem vantagens ou penalidades.";
        System.Text.StringBuilder text = new System.Text.StringBuilder(200);
        foreach (Trait trait in traits)
        {
            if (text.Length > 0) text.Append('\n');
            text.Append("<color=").Append(trait.Positive ? GoodColor : BadColor)
                .Append('>').Append(trait.Detail).Append("</color>");
        }
        return text.ToString();
    }

    public void ShowBreakWarning(float remainingFraction)
    {
        if (breakRing == null) return;
        float remaining = Mathf.Clamp01(remainingFraction);
        bool visible = remaining > 0f;
        breakRing.gameObject.SetActive(visible);

        // Os clipes controlam a imagem e o pulso; o jogo apenas informa o tempo.
        if (visible && breakWarningAnimator != null)
        {
            breakWarningAnimator.SetFloat(ClockProgressId, 1f - remaining);
            breakWarningAnimator.SetBool(ClockUrgentId, remaining <= breakAlertFraction);
        }
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
            ShowBubble(ProblemPhrase);
        }
        else
        {
            if (face != null && savedFace != null) face.sprite = savedFace;
            savedFace = null;
            // A fala de pausa tem prioridade se o funcionário sair nesse instante.
            if (ActiveBubble != null && ActiveBubbleText != null
                && ActiveBubbleText.text == ProblemPhrase) HideBubble();
        }
        RefreshAttention();
    }

    public void ShowFlowBlocked(bool visible)
    {
        if (flowBlocked == visible) return;
        flowBlocked = visible;
        if (visible && !problemShown) ShowBubble("Aguardando!");
        else if (!visible && !problemShown && ActiveBubbleText != null
            && ActiveBubbleText.text == "Aguardando!") HideBubble();
        RefreshAttention();
    }

    private void RefreshAttention()
    {
        bool visible = problemShown || flowBlocked;
        if (!visible)
        {
            attentionPulse?.Kill();
            attentionPulse = null;
            if (actor != null && attentionScaleCached)
            {
                actor.localScale = attentionActorScale;
                attentionScaleCached = false;
            }
            if (attentionMark != null)
            {
                attentionMark.gameObject.SetActive(false);
                attentionMark.rectTransform.anchoredPosition = attentionHome;
                attentionMark.rectTransform.localScale = Vector3.one;
            }
            return;
        }

        if (attentionMark == null)
        {
            GameObject marker = new GameObject("AvisoDoFuncionario", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = (RectTransform)marker.transform;
            rect.SetParent(actor, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(46f, 56f);
            attentionHome = new Vector2(0f, 59f);
            rect.anchoredPosition = attentionHome;
            attentionMark = marker.GetComponent<TMP_Text>();
            attentionMark.font = breakBubbleText != null
                ? breakBubbleText.font : TMP_Settings.defaultFontAsset;
            attentionMark.text = "!";
            attentionMark.fontSize = 52f;
            attentionMark.fontStyle = FontStyles.Bold;
            attentionMark.alignment = TextAlignmentOptions.Center;
            attentionMark.outlineColor = Color.black;
            attentionMark.outlineWidth = .22f;
            attentionMark.raycastTarget = false;
        }

        attentionMark.color = problemShown
            ? new Color32(255, 75, 65, 255) : new Color32(255, 207, 70, 255);
        attentionMark.gameObject.SetActive(true);
        if (attentionPulse != null && attentionPulse.IsActive()) return;
        attentionActorScale = actor.localScale;
        attentionScaleCached = true;
        RectTransform target = attentionMark.rectTransform;
        attentionPulse = DOTween.Sequence().SetTarget(this)
            .Append(target.DOAnchorPosX(attentionHome.x + 3f, .07f))
            .Join(target.DOScale(1.14f, .07f))
            .Join(actor.DOScale(attentionActorScale * 1.04f, .07f))
            .Append(target.DOAnchorPosX(attentionHome.x - 3f, .07f))
            .Append(target.DOAnchorPosX(attentionHome.x, .07f))
            .Join(target.DOScale(1f, .07f))
            .Join(actor.DOScale(attentionActorScale, .07f))
            .AppendInterval(1.7f).SetLoops(-1);
    }

    public bool TakeBreak(float seconds, Action returned)
    {
        if (home == null || Busy || leftExit == null || rightExit == null) return false;
        ShowStationProblem(false);
        ShowFlowBlocked(false);
        ShowBreakWarning(0f);
        MoveToForeground(pauseForegroundLayer);
        ShowBubble(pausePhrase);
        actor.DOKill();
        SetRaycasts(false);
        RectTransform exit = home.position.x < (leftExit.position.x + rightExit.position.x) * .5f
            ? leftExit : rightExit;
        Vector3 exitPosition = new Vector3(exit.position.x, actor.position.y, actor.position.z);
        route = DOTween.Sequence().SetTarget(this)
            .AppendInterval(.8f)
            .AppendCallback(HideBubble)
            .Append(actor.DOMove(exitPosition, 1.1f).SetEase(Ease.InOutSine))
            .AppendInterval(seconds)
            .OnComplete(() =>
            {
                // A estação pode ter mudado enquanto o funcionário estava fora.
                RectTransform returnAnchor = home != null ? home : exit;
                route = DOTween.Sequence().SetTarget(this)
                    .Append(actor.DOMove(returnAnchor.position, 1.1f).SetEase(Ease.InOutSine))
                    .OnComplete(() => {
                        actor.position = home != null ? home.position : exit.position;
                        RestoreDeliveryLayer();
                        route = null;
                        SetRaycasts(true);
                        returned?.Invoke();
                    });
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

    public void SetTrayItems(int mask, Sprite[] sprites = null)
    {
        if (carriedItems == null) return;
        float totalWidth = 0f;
        int itemCount = 0;
        for (int i = 0; i < carriedItems.Length; i++)
        {
            if (carriedItems[i] == null) continue;
            bool visible = (mask & (1 << i)) != 0;
            carriedItems[i].SetActive(visible);
            if (visible && sprites != null && i < sprites.Length)
                ComboCrewItemVisual.Set(carriedItems[i], sprites[i]);
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
        UpdateBubbleSide(anchor);
        if (Busy) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!actor.gameObject.activeSelf) actor.gameObject.SetActive(true);
        actor.DOKill();
        if (instant) actor.position = anchor.position;
        else actor.DOMove(anchor.position, travelSeconds).SetEase(Ease.OutCubic).SetTarget(this);
    }

#if UNITY_EDITOR
    public void PreviewAt(RectTransform anchor, bool showBubble, bool showTray, bool showRing)
    {
        if (Application.isPlaying || anchor == null || actor == null) return;
        if (!actor.gameObject.activeSelf) actor.gameObject.SetActive(true);
        if (actor.position != anchor.position) actor.position = anchor.position;
        bubbleOnRight = anchor.GetComponentInParent<ComboCrewStationView>()
            ?.transform.localPosition.x < -1f;
        GameObject visibleBubble = ActiveBubble;
        if (breakBubble != null && breakBubble != visibleBubble)
            breakBubble.SetActive(false);
        if (breakBubbleRight != null && breakBubbleRight != visibleBubble)
            breakBubbleRight.SetActive(false);
        if (visibleBubble != null)
        {
            visibleBubble.SetActive(showBubble);
            var group = visibleBubble.GetComponent<CanvasGroup>();
            if (group != null && group.alpha != 1f) group.alpha = 1f;
            if (visibleBubble.transform.localScale != Vector3.one)
                visibleBubble.transform.localScale = Vector3.one;
        }
        if (carriedTray != null) carriedTray.SetActive(showTray);
        if (breakRing != null)
        {
            breakRing.gameObject.SetActive(showRing);
            if (showRing && breakRing.fillAmount <= 0f) breakRing.fillAmount = .65f;
        }
    }
#endif

    private void UpdateBubbleSide(RectTransform anchor)
    {
        var station = anchor.GetComponentInParent<ComboCrewStationView>();
        bool nextSide = station != null && station.transform.localPosition.x < -1f;
        if (nextSide == bubbleOnRight) return;
        HideBubbleImmediately();
        bubbleOnRight = nextSide;
    }

    private void ShowBubble(string message)
    {
        GameObject bubble = ActiveBubble;
        TMP_Text bubbleText = ActiveBubbleText;
        if (bubble == null || bubbleText == null) return;
        bubbleTween?.Kill();
        bubbleText.text = message;
        bubble.SetActive(true);
        var rect = bubble.transform as RectTransform;
        var group = bubble.GetComponent<CanvasGroup>();
        if (rect == null || group == null) return;
        rect.localScale = new Vector3(.15f, .85f, 1f);
        group.alpha = 0f;
        bubbleTween = DOTween.Sequence().SetTarget(this).SetUpdate(true)
            .Join(rect.DOScale(Vector3.one, .23f).SetEase(Ease.OutBack))
            .Join(group.DOFade(1f, .16f));
    }

    private void HideBubble()
    {
        GameObject bubble = ActiveBubble;
        if (bubble == null || !bubble.activeSelf) return;
        bubbleTween?.Kill();
        var rect = bubble.transform as RectTransform;
        var group = bubble.GetComponent<CanvasGroup>();
        if (rect == null || group == null) { bubble.SetActive(false); return; }
        bubbleTween = DOTween.Sequence().SetTarget(this).SetUpdate(true)
            .Join(rect.DOScale(new Vector3(.2f, .85f, 1f), .13f).SetEase(Ease.InCubic))
            .Join(group.DOFade(0f, .13f))
            .OnComplete(() => { if (bubble != null) bubble.SetActive(false); });
    }

    private void HideBubbleImmediately()
    {
        bubbleTween?.Kill();
        bubbleTween = null;
        if (breakBubble != null) breakBubble.SetActive(false);
        if (breakBubbleRight != null) breakBubbleRight.SetActive(false);
    }

    public void ReturnToReserveAfterBreak()
    {
        home = null;
    }

    public void Travel(RectTransform destination, Transform foregroundLayer,
        Action handedOff, Action completed)
    {
        if (!Configured || home == null || destination == null || Busy) return;
        ShowFlowBlocked(false);
        ShowStationProblem(false);
        MoveToForeground(foregroundLayer);
        float transportSeconds = travelSeconds / TransportSpeed;
        SetTrayItems(0);
        carriedTray.SetActive(true);
        route = DOTween.Sequence().SetTarget(this)
            .Append(actor.DOMove(destination.position, transportSeconds).SetEase(Ease.InOutCubic))
            .AppendInterval(handoffSeconds)
            .AppendCallback(() => carriedTray.SetActive(false))
            .AppendCallback(() => handedOff?.Invoke())
            .Append(actor.DOMove(home.position, transportSeconds).SetEase(Ease.InOutCubic))
            .OnComplete(() => { RestoreDeliveryLayer(); route = null; completed?.Invoke(); });
    }

    public void Deliver(RectTransform destination, Transform foregroundLayer,
        Action delivered, Action returned)
    {
        if (!Configured || home == null || destination == null || Busy) return;
        ShowFlowBlocked(false);
        ShowStationProblem(false);
        MoveToForeground(foregroundLayer);
        float transportSeconds = travelSeconds * 1.6f / TransportSpeed;
        carriedTray.SetActive(true);
        route = DOTween.Sequence().SetTarget(this)
            .Append(actor.DOMove(destination.position, transportSeconds).SetEase(Ease.InOutCubic))
            .AppendCallback(() =>
            {
                carriedTray.SetActive(false);
                delivered?.Invoke();
                SetTrayItems(0); // Retorna com a bandeja suja, agora vazia.
            })
            .AppendInterval(deliveryWaitSeconds)
            .AppendCallback(() => carriedTray.SetActive(true))
            .Append(actor.DOMove(home.position, transportSeconds).SetEase(Ease.InOutCubic))
            .AppendCallback(() => carriedTray.SetActive(false))
            .OnComplete(() => { RestoreDeliveryLayer(); route = null; returned?.Invoke(); });
    }

    private void MoveToForeground(Transform foregroundLayer)
    {
        if (foregroundLayer == null || actor == null) return;
        if (deliveryHomeParent == null)
        {
            deliveryHomeParent = actor.parent;
            deliveryHomeSibling = actor.GetSiblingIndex();
        }
        if (actor.parent != foregroundLayer) actor.SetParent(foregroundLayer, true);
        actor.SetAsLastSibling();
    }

    private void RestoreDeliveryLayer()
    {
        if (actor == null || deliveryHomeParent == null) return;
        actor.SetParent(deliveryHomeParent, true);
        actor.SetSiblingIndex(Mathf.Clamp(deliveryHomeSibling, 0, deliveryHomeParent.childCount - 1));
        deliveryHomeParent = null;
        restoreLayerWhenActive = false;
    }

    private void LateUpdate()
    {
        if (restoreLayerWhenActive && actor != null && actor.gameObject.activeInHierarchy
            && deliveryHomeParent != null && deliveryHomeParent.gameObject.activeInHierarchy)
            RestoreDeliveryLayer();
    }

    public bool Wash(RectTransform counter, RectTransform firstStation, int count,
        float secondsPerTray, Action collected, Action completed,
        float progress = 0f, bool alreadyCollected = false)
    {
        if (!Configured || home == null || counter == null || firstStation == null || Busy || count < 1) return false;
        actor.DOKill();
        MoveToForeground(pauseForegroundLayer);
        WashingAtSink = false;
        WashProgress = Mathf.Clamp01(progress);
        SetTrayItems(0);
        route = DOTween.Sequence().SetTarget(this);
        if (!alreadyCollected)
        {
            route.Append(actor.DOMove(counter.position, travelSeconds + count * .08f).SetEase(Ease.InOutCubic))
                .AppendCallback(() => carriedTray.SetActive(true))
                .AppendCallback(() => collected?.Invoke())
                .Append(actor.DOMove(home.position, travelSeconds + count * .08f).SetEase(Ease.InOutCubic));
        }
        else
        {
            carriedTray.SetActive(true);
            route.Append(actor.DOMove(home.position, travelSeconds).SetEase(Ease.InOutCubic));
        }
        route.AppendCallback(() => WashingAtSink = true)
            .Append(DOTween.To(() => WashProgress, value => WashProgress = value,
                1f, Mathf.Max(.01f, secondsPerTray * count * (1f - WashProgress))).SetEase(Ease.Linear))
            .AppendCallback(() => WashingAtSink = false)
            .Append(actor.DOMove(firstStation.position, travelSeconds + count * .08f).SetEase(Ease.InOutCubic))
            .AppendCallback(() => carriedTray.SetActive(false))
            .Append(actor.DOMove(home.position, travelSeconds).SetEase(Ease.InOutCubic))
            .OnComplete(() => { WashingAtSink = false; RestoreDeliveryLayer(); route = null; completed?.Invoke(); });
        return true;
    }

    /// <summary>Interrompe o transporte; o controlador mantém o trabalho na estação.</summary>
    public void StopWorkRoute()
    {
        route?.Kill(false);
        route = null;
        WashingAtSink = false;
        actor?.DOKill();
        RestoreDeliveryLayer();
        if (carriedTray != null) carriedTray.SetActive(false);
        SetTrayItems(0);
    }

    public void SetPaused(bool value)
    {
        if (breakWarningAnimator != null) breakWarningAnimator.speed = value ? 0f : 1f;
        if (!Busy) return;
        if (value) route.Pause();
        else route.Play();
    }

    private void OnDisable()
    {
        bubbleTween?.Kill();
        bubbleTween = null;
        // A Unity pode chamar OnDisable enquanto o pai ainda está sendo desativado.
        // Mudar a hierarquia nesse callback gera erro; a restauração fica para LateUpdate.
        restoreLayerWhenActive = deliveryHomeParent != null;
        WashingAtSink = false;
        ShowStationProblem(false);
        ShowFlowBlocked(false);
        if (actorGroup != null) SetAbsent(false);
        route?.Kill();
        route = null;
        actor?.DOKill();
        if (carriedTray != null) carriedTray.SetActive(false);
        HideBubbleImmediately();
        if (breakRing != null)
        {
            breakRing.gameObject.SetActive(false);
        }
        SetTrayItems(0);
        if (actorGroup != null) actorGroup.blocksRaycasts = originalRaycasts;
        if (actor != null && home != null) actor.position = home.position;
    }
}
