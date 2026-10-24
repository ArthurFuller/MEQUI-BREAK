using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RushCustomerView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    public enum PatienceProfile { Patient, Normal, Demanding }

    [SerializeField] private RectTransform character;
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image torsoImage;
    [SerializeField] private Image fullBodyImage;
    [SerializeField] private Image faceCover;
    [SerializeField] private Image hatImage;
    [SerializeField, Range(.4f, 1f)] private float fullBodyHatScale = .9f;
    [SerializeField] private Vector2 fullBodyHatOffset = new Vector2(0f, -.03f);
    [SerializeField, Min(.01f)] private float hatScaleMultiplier = .78f;
    [SerializeField] private GameObject balloon;
    [SerializeField] private CanvasGroup balloonGroup;
    [SerializeField, Min(0f)] private float orderIconSpacing = 56f;
    [SerializeField] private Button orderButton;
    [SerializeField] private GameObject patienceTrack;
    [SerializeField] private Image patienceFill;
    [SerializeField] private bool circularPatience;
    private Tween patiencePulse;
    private Vector3 patienceBaseScale = Vector3.one;
    [SerializeField] private TMP_Text speech;
    [Tooltip("Três ícones já configurados na Hierarchy.")]
    [SerializeField] private GameObject[] itemIcons;
    [Tooltip("Ordem: feliz, sério e frustrado.")]
    [SerializeField] private GameObject[] faces;
    [Header("Movimento e paciência do cliente")]
    [SerializeField, Min(.01f)] private float movementSeconds = .32f;
    [SerializeField] private Ease enterEase = Ease.OutCubic;
    [Tooltip("Duracao da chegada. Zero usa o tempo de movimento padrao.")]
    [SerializeField, Min(0f)] private float arrivalMovementSeconds;
    [SerializeField] private Ease arrivalEase = Ease.InOutSine;
    [SerializeField] private Ease exitEase = Ease.InCubic;
    [SerializeField, Min(1f)] private float barSmoothing = 12f;
    [Header("Interação com o balão")]
    [SerializeField, Min(1f)] private float balloonHoverScale = 1.04f;
    [SerializeField, Min(.01f)] private float balloonHoverSeconds = .1f;
    [SerializeField] private Ease balloonHoverEase = Ease.OutQuad;
    [SerializeField, Range(.85f, 1f)] private float balloonPressScale = .94f;
    [SerializeField, Min(.01f)] private float balloonPressSeconds = .06f;
    [SerializeField] private Ease balloonPressEase = Ease.OutQuad;
    [SerializeField, Min(.01f)] private float balloonReleaseSeconds = .12f;
    [SerializeField] private Ease balloonReleaseEase = Ease.OutBack;
    [SerializeField, Min(0f)] private float balloonClickPunch = .07f;
    [SerializeField, Min(0f)] private float initialBalloonPopStrength = .055f;
    [Header("Reações do cliente")]
    [SerializeField, Min(0f)] private float moodPulseStrength = .05f;
    [SerializeField, Min(.01f)] private float moodPulseSeconds = .18f;
    [SerializeField, Min(0f)] private float deliveryReactionStrength = .075f;
    [SerializeField, Min(.01f)] private float deliveryReactionSeconds = .2f;
    [SerializeField, Min(0f)] private float failedDeliveryPause = .08f;
    [SerializeField, Min(0f)] private float punchElasticity = .35f;
    [SerializeField, Min(1)] private int punchVibrato = 2;
    [Header("Mensagens")]
    [SerializeField] private string happyMessage = "Oi, tudo bem?";
    [SerializeField] private string seriousMessage = "Vai demorar muito?";
    [SerializeField] private string angryMessage = "Pode me atender?";

    private readonly Sprite[] faceSprites = new Sprite[3];
    private RushBalanceController owner;
    private Tween movement;
    private Action arrivalCompleted;
    private Tween moodFeedback;
    private Tween balloonFeedback;
    private Tween bubbleTween;
    private Tween speechTimeout;
    private bool showingOrder;
    private bool BubbleAnimating => bubbleTween != null && bubbleTween.IsActive();
    private Vector3 characterBaseScale;
    private Vector3 balloonBaseScale;
    private float patienceDisplayed = 1f;
    private static readonly Color HealthyPatience = new Color(.34f, .72f, .39f);
    private RectTransform balloonRect;
    private float patienceTarget = 1f;
    private float patienceSecondsRemaining;
    private bool patienceRunning;
    private int customerIndex;
    private int mood = -1;
    private bool departing;
    private bool balloonHovered;
    private bool balloonPressed;

    public int OrderId { get; private set; } = -1;
    public bool IsDeparting => departing;
    public RectTransform DeliveryTarget => character;
    public Color BodyColor => bodyImage != null ? bodyImage.color : Color.white;
    public Image HatImage => hatImage;
    public float HatScaleMultiplier => hatScaleMultiplier
        * (fullBodyImage != null && fullBodyImage.enabled ? fullBodyHatScale : 1f);
    public Vector2 HatModeOffset => fullBodyImage != null && fullBodyImage.enabled
        ? fullBodyHatOffset : Vector2.zero;
    public bool HasAppearance { get; private set; }
    public int HatIndex { get; private set; } = -1;
    public int TorsoIndex { get; private set; } = -1;
    public int ColorIndex { get; private set; } = -1;
    public Sprite CurrentFaceSprite => mood >= 0 && mood < faceSprites.Length ? faceSprites[mood] : null;
    public PatienceProfile Profile { get; private set; }
    public string ConfigurationError =>
        character == null ? "character" :
        bodyImage == null ? "bodyImage" :
        torsoImage == null ? "torsoImage" :
        fullBodyImage == null ? "fullBodyImage" :
        faceCover == null ? "faceCover" :
        hatImage == null ? "hatImage" :
        balloon == null ? "balloon" :
        orderButton == null ? "orderButton" :
        patienceTrack == null ? "patienceTrack" :
        patienceFill == null ? "patienceFill" :
        speech == null ? "speech" :
        itemIcons == null || itemIcons.Length != 3 ? "itemIcons: esperado 3 elementos" :
        itemIcons[0] == null ? "itemIcons[0]" :
        itemIcons[1] == null ? "itemIcons[1]" :
        itemIcons[2] == null ? "itemIcons[2]" :
        faces == null || faces.Length != 3 ? "faces: esperado 3 elementos" :
        faces[0] == null ? "faces[0]" :
        faces[1] == null ? "faces[1]" :
        faces[2] == null ? "faces[2]" :
        null;
    public bool Configured => ConfigurationError == null;

    public void Initialize(RushBalanceController controller, int index)
    {
        owner = controller;
        customerIndex = index;
        characterBaseScale = character.localScale;
        balloonRect = balloon.transform as RectTransform;
        balloonBaseScale = balloonRect != null ? balloonRect.localScale : Vector3.one;
        patienceFill.type = Image.Type.Filled;
        patienceBaseScale = patienceTrack.transform.localScale;
        patienceFill.fillMethod = circularPatience ? Image.FillMethod.Radial360 : Image.FillMethod.Horizontal;
        patienceFill.fillOrigin = circularPatience ? (int)Image.Origin360.Top : (int)Image.OriginHorizontal.Left;
        patienceFill.fillClockwise = true;
        patienceFill.fillAmount = 1f;
        patienceFill.color = HealthyPatience;

        for (int i = 0; i < faces.Length; i++)
        {
            Image image = faces[i].GetComponent<Image>();
            faceSprites[i] = image != null ? image.sprite : null;
        }

        orderButton.onClick.AddListener(SelectOrder);
        Clear();
    }

    private void Update()
    {
        UpdatePatienceDisplay(Time.unscaledDeltaTime);
        bool warn = patienceRunning && circularPatience && patienceTarget > 0f && patienceTarget <= .2f
            && patienceTrack.activeInHierarchy;
        if (warn && (patiencePulse == null || !patiencePulse.IsActive()))
            patiencePulse = patienceTrack.transform.DOScale(patienceBaseScale * 1.06f, .45f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(this);
        else if (!warn) StopPatiencePulse();
    }

    private void UpdatePatienceDisplay(float deltaTime)
    {
        if (circularPatience)
            patienceDisplayed = patienceTarget;
        else if (deltaTime > 0f)
            patienceDisplayed = Mathf.Lerp(patienceDisplayed, patienceTarget, 1f - Mathf.Exp(-barSmoothing * deltaTime));
        patienceFill.fillAmount = patienceDisplayed;
    }

    public void Clear()
    {
        arrivalCompleted = null;
        StopPatiencePulse();
        movement?.Kill();
        moodFeedback?.Kill();
        balloonFeedback?.Kill();
        bubbleTween?.Kill();
        speechTimeout?.Kill();
        showingOrder = false;
        OrderId = -1;
        HasAppearance = false;
        HatIndex = -1;
        TorsoIndex = -1;
        ColorIndex = -1;
        departing = false;
        balloonHovered = false;
        balloonPressed = false;
        mood = -1;
        character.localScale = characterBaseScale;
        if (balloonRect != null) balloonRect.localScale = balloonBaseScale;
        patienceDisplayed = 1f;
        if (patienceFill != null)
        {
            patienceFill.fillAmount = 1f;
            patienceFill.color = HealthyPatience;
        }
        patienceTarget = 1f;
        patienceSecondsRemaining = 0f;
        patienceRunning = false;
        gameObject.SetActive(false);
    }

    public void ApplyAppearance(RushBalanceController.NpcAppearance appearance)
    {
        HasAppearance = true;
        HatIndex = appearance.HatIndex;
        TorsoIndex = appearance.TorsoIndex;
        ColorIndex = appearance.ColorIndex;
        bool useFullBody = appearance.FullBodySprite != null;
        bodyImage.color = appearance.BodyColor;
        bodyImage.enabled = !useFullBody;
        torsoImage.sprite = appearance.TorsoSprite;
        torsoImage.color = Color.white;
        torsoImage.enabled = !useFullBody;
        fullBodyImage.sprite = appearance.FullBodySprite;
        fullBodyImage.color = Color.white;
        fullBodyImage.enabled = useFullBody;
        faceCover.color = appearance.BodyColor;
        faceCover.enabled = useFullBody;
        hatImage.sprite = appearance.HatSprite;
        hatImage.enabled = appearance.HatSprite != null;
        AvatarView.StyleCustomizationIcon(hatImage, AvatarCustomizationCategory.Hat);
        AvatarView.ApplyNormalizedTransform(
            hatImage,
            appearance.HatOffset + (useFullBody ? fullBodyHatOffset : Vector2.zero),
            appearance.HatScale * hatScaleMultiplier * (useFullBody ? fullBodyHatScale : 1f),
            appearance.HatRotation);
    }

    public void SetFoodSprites(Sprite[] sprites)
    {
        for (int i = 0; i < sprites.Length && i < itemIcons.Length; i++)
        {
            Image image = itemIcons[i].GetComponentInChildren<Image>(true);
            if (image != null) image.sprite = sprites[i];
        }
    }

    public void Enter(
        int orderId,
        RushRound.Order order,
        PatienceProfile profile,
        RectTransform entrance,
        RectTransform target,
        bool instant,
        Action onArrived = null)
    {
        movement?.Kill();
        arrivalCompleted = onArrived;
        moodFeedback?.Kill();
        balloonFeedback?.Kill();
        bubbleTween?.Kill();
        speechTimeout?.Kill();
        showingOrder = true;
        OrderId = orderId;
        Profile = profile;
        mood = -1;
        departing = false;
        balloonHovered = false;
        balloonPressed = false;
        patienceFill.enabled = true;
        patienceFill.fillAmount = 1f;
        patienceFill.color = HealthyPatience;
        patienceFill.transform.SetAsLastSibling();
        gameObject.SetActive(true);
        character.gameObject.SetActive(true);
        character.localScale = characterBaseScale;
        balloon.SetActive(true);
        patienceTrack.SetActive(true);
        patienceFill.gameObject.SetActive(true);
        speech.gameObject.SetActive(false);
        speech.enabled = false;
        patienceDisplayed = 1f;
        patienceFill.fillAmount = 1f;
        patienceTarget = 1f;
        character.position = instant ? target.position : entrance.position;
        for (int i = 0; i < itemIcons.Length; i++)
        {
            itemIcons[i].SetActive(i < order.Items);
            if (balloonGroup != null)
                ((RectTransform)itemIcons[i].transform).anchoredPosition =
                    new Vector2((i - (order.Items - 1) * .5f) * orderIconSpacing, 0f);
        }
        Refresh(order);
        if (balloonGroup != null) ShowBubble();
        else PlayBalloonPop(initialBalloonPopStrength);
        MoveTo(target, instant);
    }

    public void MoveTo(RectTransform target, bool instant = false)
    {
        if (!gameObject.activeSelf || target == null || departing) return;
        movement?.Kill();
        if (instant)
        {
            character.position = target.position;
            CompleteArrival();
        }
        else
        {
            bool entering = arrivalCompleted != null && arrivalMovementSeconds > 0f;
            movement = character.DOMove(target.position,
                    Seconds(entering ? arrivalMovementSeconds : movementSeconds))
                .SetEase(entering ? arrivalEase : enterEase)
                .OnComplete(CompleteArrival);
        }
    }

    private void CompleteArrival()
    {
        Action callback = arrivalCompleted;
        arrivalCompleted = null;
        callback?.Invoke();
    }

    public void Refresh(RushRound.Order order)
    {
        if (!gameObject.activeSelf || departing) return;

        bool active = order.State == RushRound.Status.Waiting || order.State == RushRound.Status.Queued
            || order.State == RushRound.Status.Ready;
        patienceSecondsRemaining = Mathf.Max(0f, order.Remaining);
        float ratio = order.Patience > 0f ? Mathf.Clamp01(patienceSecondsRemaining / order.Patience) : 0f;
        patienceTarget = ratio;
        patienceRunning = active && patienceSecondsRemaining > 0f;
        if (!patienceRunning) StopPatiencePulse();
        if (order.State == RushRound.Status.Delivered) patienceTrack.SetActive(false);
        UpdatePatienceDisplay(0f);
        patienceFill.color = ratio > .65f
            ? new Color(.34f, .72f, .39f)
            : ratio > .3f ? new Color(1f, .72f, .12f) : new Color(.9f, .25f, .18f);

        int nextMood = ratio > .65f ? 0 : ratio > .3f ? 1 : 2;
        bool moodChanged = mood != nextMood;
        if (moodChanged) SetMood(nextMood, mood >= 0);

        bool waiting = order.State == RushRound.Status.Waiting;
        if (balloonGroup != null)
        {
            orderButton.interactable = waiting && !order.Arriving;
            if (waiting)
            {
                speech.gameObject.SetActive(false);
                speech.enabled = false;
                for (int i = 0; i < itemIcons.Length; i++) itemIcons[i].SetActive(i < order.Items);
            }
            else if (showingOrder)
            {
                showingOrder = false;
                HideBubble();
            }
            else if (moodChanged && (order.State == RushRound.Status.Queued || order.State == RushRound.Status.Ready))
            {
                foreach (GameObject icon in itemIcons) icon.SetActive(false);
                speech.gameObject.SetActive(true);
                speech.enabled = true;
                ShowBubble();
            }
            return;
        }
        bool speechWasVisible = speech.gameObject.activeSelf;
        orderButton.interactable = waiting && !order.Arriving;
        speech.gameObject.SetActive(!waiting);
        speech.enabled = !waiting;
        for (int i = 0; i < itemIcons.Length; i++)
            itemIcons[i].SetActive(waiting && i < order.Items);
        if (!waiting && !speechWasVisible) PlayBalloonPop(balloonClickPunch);
    }

    private void SetMood(int nextMood, bool animate)
    {
        mood = nextMood;
        for (int i = 0; i < faces.Length; i++) faces[i].SetActive(i == mood);
        speech.text = mood == 0 ? happyMessage : mood == 1 ? seriousMessage : angryMessage;
        if (!animate) return;
        moodFeedback?.Kill();
        character.localScale = characterBaseScale;
        moodFeedback = character.DOPunchScale(Vector3.one * moodPulseStrength, Seconds(moodPulseSeconds), punchVibrato, punchElasticity)
            .OnComplete(() => character.localScale = characterBaseScale);
    }

    public void Depart(RectTransform exit, bool delivered, Action completed)
    {
        if (departing) return;
        arrivalCompleted = null;
        departing = true;
        StopPatiencePulse();
        bubbleTween?.Kill();
        speechTimeout?.Kill();
        balloon.SetActive(false);
        patienceTrack.SetActive(false);
        patienceFill.gameObject.SetActive(false);
        movement?.Kill();
        moodFeedback?.Kill();
        character.localScale = characterBaseScale;

        Sequence sequence = DOTween.Sequence();
        if (delivered)
        {
            SetMood(0, false);
            sequence.Append(character.DOPunchScale(Vector3.one * deliveryReactionStrength, Seconds(deliveryReactionSeconds), punchVibrato, punchElasticity));
        }
        else
        {
            SetMood(2, false);
            sequence.AppendInterval(Seconds(failedDeliveryPause));
        }

        sequence.Append(character.DOMove(exit.position, Seconds(movementSeconds)).SetEase(exitEase))
            .OnComplete(() =>
            {
                character.localScale = characterBaseScale;
                gameObject.SetActive(false);
                departing = false;
                OrderId = -1;
                completed?.Invoke();
            });
        movement = sequence;
    }

    private void SelectOrder()
    {
        if (owner != null) owner.Queue(OrderId);
    }

#if UNITY_EDITOR
    public void SetHatEditorPreview(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible) return;

        character.gameObject.SetActive(true);
        balloon.SetActive(false);
        patienceTrack.SetActive(false);
        patienceFill.gameObject.SetActive(false);
        for (int i = 0; i < faces.Length; i++)
            faces[i].SetActive(i == 0);
    }
#endif

    public bool PulseStartHint()
    {
        if (!showingOrder || departing || balloonRect == null || !balloon.activeInHierarchy
            || !orderButton.IsInteractable() || BubbleAnimating || balloonHovered || balloonPressed
            || (balloonFeedback != null && balloonFeedback.IsActive())) return false;
        balloonFeedback = DOTween.Sequence().SetUpdate(true).SetTarget(this)
            .Append(balloonRect.DOScale(balloonBaseScale * 1.08f, .32f).SetEase(Ease.InOutSine))
            .Append(balloonRect.DOScale(balloonBaseScale, .32f).SetEase(Ease.InOutSine));
        return true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!orderButton.interactable || BubbleAnimating || balloonRect == null) return;
        balloonHovered = true;
        if (balloonPressed) return;
        balloonFeedback?.Kill();
        balloonFeedback = balloonRect
            .DOScale(balloonBaseScale * balloonHoverScale, Seconds(balloonHoverSeconds))
            .SetEase(balloonHoverEase);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!orderButton.interactable || BubbleAnimating || balloonRect == null) return;
        balloonHovered = false;
        if (balloonPressed) return;
        balloonFeedback?.Kill();
        balloonFeedback = balloonRect
            .DOScale(balloonBaseScale, Seconds(balloonHoverSeconds))
            .SetEase(balloonHoverEase);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!orderButton.interactable || BubbleAnimating || balloonRect == null) return;
        balloonPressed = true;
        balloonFeedback?.Kill();
        balloonFeedback = balloonRect
            .DOScale(balloonBaseScale * balloonPressScale, Seconds(balloonPressSeconds))
            .SetEase(balloonPressEase);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!balloonPressed || balloonRect == null) return;
        balloonPressed = false;
        balloonFeedback?.Kill();
        Vector3 target = balloonBaseScale * (balloonHovered && orderButton.interactable
            ? balloonHoverScale : 1f);
        balloonFeedback = balloonRect
            .DOScale(target, Seconds(balloonReleaseSeconds))
            .SetEase(balloonReleaseEase);
    }

    private void ShowBubble()
    {
        bubbleTween?.Kill();
        speechTimeout?.Kill();
        balloonFeedback?.Kill();
        balloonHovered = balloonPressed = false;
        balloon.SetActive(true);
        balloonRect.localScale = Vector3.Scale(balloonBaseScale, new Vector3(.15f, .85f, 1f));
        balloonGroup.alpha = 0f;
        balloonGroup.blocksRaycasts = false;
        bubbleTween = DOTween.Sequence().SetTarget(this).SetUpdate(true)
            .Join(balloonRect.DOScale(balloonBaseScale, .23f).SetEase(Ease.OutBack))
            .Join(balloonGroup.DOFade(1f, .16f))
            .OnComplete(() => balloonGroup.blocksRaycasts = showingOrder);
        // Inclui os .13s do fechamento no limite total de cinco segundos.
        if (!showingOrder)
            speechTimeout = DOVirtual.DelayedCall(4.87f, HideBubble).SetTarget(this);
    }

    private void HideBubble()
    {
        bubbleTween?.Kill();
        speechTimeout?.Kill();
        balloonFeedback?.Kill();
        balloonHovered = balloonPressed = false;
        balloonGroup.blocksRaycasts = false;
        bubbleTween = DOTween.Sequence().SetTarget(this).SetUpdate(true)
            .Join(balloonRect.DOScale(Vector3.Scale(balloonBaseScale, new Vector3(.2f, .85f, 1f)), .13f).SetEase(Ease.InCubic))
            .Join(balloonGroup.DOFade(0f, .13f))
            .OnComplete(() => balloon.SetActive(false));
    }

    private void PlayBalloonPop(float strength)
    {
        if (balloonRect == null) return;
        balloonFeedback?.Kill();
        balloonHovered = false;
        balloonPressed = false;
        balloonRect.localScale = balloonBaseScale;
        balloonFeedback = balloonRect
            .DOPunchScale(Vector3.one * strength, Seconds(.2f), 2, .35f)
            .OnComplete(() => balloonRect.localScale = balloonBaseScale);
    }

    private float Seconds(float duration)
    {
        return owner != null ? owner.AnimationSeconds(duration) : duration;
    }

    private void StopPatiencePulse()
    {
        if (patiencePulse == null) return;
        patiencePulse.Kill();
        patiencePulse = null;
        if (patienceTrack != null) patienceTrack.transform.localScale = patienceBaseScale;
    }

    private void OnDisable()
    {
        arrivalCompleted = null;
        StopPatiencePulse();
        movement?.Kill();
        moodFeedback?.Kill();
        balloonFeedback?.Kill();
        bubbleTween?.Kill();
        speechTimeout?.Kill();
        if (balloonGroup != null) balloonGroup.alpha = 1f;
        if (character != null) character.localScale = characterBaseScale;
        if (balloonRect != null) balloonRect.localScale = balloonBaseScale;
    }

    private void OnDestroy()
    {
        if (orderButton != null) orderButton.onClick.RemoveListener(SelectOrder);
    }
}
