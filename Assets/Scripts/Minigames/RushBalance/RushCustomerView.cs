using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RushCustomerView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public enum PatienceProfile { Patient, Normal, Demanding }

    [SerializeField] private RectTransform character;
    [SerializeField] private Image bodyImage;
    [SerializeField] private GameObject balloon;
    [SerializeField] private Button orderButton;
    [SerializeField] private Image patienceFill;
    [SerializeField] private TMP_Text speech;
    [Tooltip("Três ícones já configurados na Hierarchy.")]
    [SerializeField] private GameObject[] itemIcons;
    [Tooltip("Ordem: feliz, sério e frustrado.")]
    [SerializeField] private GameObject[] faces;
    [SerializeField, Min(.01f)] private float movementSeconds = .32f;
    [SerializeField, Min(1f)] private float barSmoothing = 12f;
    [SerializeField, Min(1f)] private float balloonHoverScale = 1.04f;
    [SerializeField, Min(.01f)] private float balloonHoverSeconds = .1f;
    [SerializeField, Min(0f)] private float balloonClickPunch = .07f;
    [SerializeField] private string happyMessage = "Oi, tudo bem?";
    [SerializeField] private string seriousMessage = "Vai demorar muito?";
    [SerializeField] private string angryMessage = "Pode me atender?";

    private readonly Sprite[] faceSprites = new Sprite[3];
    private RushBalanceController owner;
    private Tween movement;
    private Tween moodFeedback;
    private Tween balloonFeedback;
    private Vector3 characterBaseScale;
    private Vector3 balloonBaseScale;
    private Vector3 patienceFullScale;
    private RectTransform patienceBar;
    private RectTransform balloonRect;
    private float patienceTarget = 1f;
    private int customerIndex;
    private int mood = -1;
    private bool departing;

    public int OrderId { get; private set; } = -1;
    public bool IsDeparting => departing;
    public RectTransform DeliveryTarget => character;
    public Color BodyColor => bodyImage != null ? bodyImage.color : Color.white;
    public Sprite CurrentFaceSprite => mood >= 0 && mood < faceSprites.Length ? faceSprites[mood] : null;
    public PatienceProfile Profile { get; private set; }
    public string ConfigurationError =>
        character == null ? "character" :
        bodyImage == null ? "bodyImage" :
        balloon == null ? "balloon" :
        orderButton == null ? "orderButton" :
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
        patienceBar = patienceFill.rectTransform;
        patienceFullScale = patienceBar.localScale;
        patienceFill.type = Image.Type.Simple;

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
        if (patienceBar == null) return;
        float blend = 1f - Mathf.Exp(-barSmoothing * Time.unscaledDeltaTime);
        Vector3 scale = patienceBar.localScale;
        scale.x = Mathf.Lerp(scale.x, patienceFullScale.x * patienceTarget, blend);
        patienceBar.localScale = scale;
    }

    public void Clear()
    {
        movement?.Kill();
        moodFeedback?.Kill();
        balloonFeedback?.Kill();
        OrderId = -1;
        departing = false;
        mood = -1;
        character.localScale = characterBaseScale;
        if (balloonRect != null) balloonRect.localScale = balloonBaseScale;
        if (patienceBar != null) patienceBar.localScale = patienceFullScale;
        patienceTarget = 1f;
        gameObject.SetActive(false);
    }

    public void Enter(
        int orderId,
        RushRound.Order order,
        PatienceProfile profile,
        RectTransform entrance,
        RectTransform target,
        bool instant)
    {
        movement?.Kill();
        moodFeedback?.Kill();
        balloonFeedback?.Kill();
        OrderId = orderId;
        Profile = profile;
        mood = -1;
        departing = false;
        gameObject.SetActive(true);
        character.gameObject.SetActive(true);
        character.localScale = characterBaseScale;
        balloon.SetActive(true);
        patienceFill.gameObject.SetActive(true);
        speech.gameObject.SetActive(false);
        speech.enabled = false;
        patienceBar.localScale = patienceFullScale;
        patienceTarget = 1f;
        character.position = instant ? target.position : entrance.position;
        for (int i = 0; i < itemIcons.Length; i++) itemIcons[i].SetActive(i < order.Items);
        Refresh(order);
        PlayBalloonPop(.055f);
        MoveTo(target, instant);
    }

    public void MoveTo(RectTransform target, bool instant = false)
    {
        if (!gameObject.activeSelf || target == null || departing) return;
        movement?.Kill();
        if (instant) character.position = target.position;
        else movement = character.DOMove(target.position, Seconds(movementSeconds)).SetEase(Ease.OutCubic);
    }

    public void Refresh(RushRound.Order order)
    {
        if (!gameObject.activeSelf || departing) return;

        float ratio = Mathf.Clamp01(order.Remaining / order.Patience);
        patienceTarget = ratio;
        patienceFill.color = ratio > .65f
            ? new Color(.34f, .72f, .39f)
            : ratio > .3f ? new Color(1f, .72f, .12f) : new Color(.9f, .25f, .18f);

        int nextMood = ratio > .65f ? 0 : ratio > .3f ? 1 : 2;
        if (mood != nextMood) SetMood(nextMood, mood >= 0);

        bool waiting = order.State == RushRound.Status.Waiting;
        bool speechWasVisible = speech.gameObject.activeSelf;
        orderButton.interactable = waiting;
        speech.gameObject.SetActive(!waiting);
        speech.enabled = !waiting;
        for (int i = 0; i < itemIcons.Length; i++)
            itemIcons[i].SetActive(waiting && i < order.Items);
        if (!waiting && !speechWasVisible) PlayBalloonPop(.07f);
    }

    private void SetMood(int nextMood, bool animate)
    {
        mood = nextMood;
        for (int i = 0; i < faces.Length; i++) faces[i].SetActive(i == mood);
        speech.text = mood == 0 ? happyMessage : mood == 1 ? seriousMessage : angryMessage;
        if (!animate) return;
        moodFeedback?.Kill();
        character.localScale = characterBaseScale;
        moodFeedback = character.DOPunchScale(Vector3.one * .05f, Seconds(.18f), 2, .35f)
            .OnComplete(() => character.localScale = characterBaseScale);
    }

    public void Depart(RectTransform exit, bool delivered, Action completed)
    {
        if (departing) return;
        departing = true;
        balloon.SetActive(false);
        patienceFill.gameObject.SetActive(false);
        movement?.Kill();
        moodFeedback?.Kill();
        character.localScale = characterBaseScale;

        Sequence sequence = DOTween.Sequence();
        if (delivered)
        {
            SetMood(0, false);
            sequence.Append(character.DOPunchScale(Vector3.one * .075f, Seconds(.2f), 2, .35f));
        }
        else
        {
            SetMood(2, false);
            sequence.AppendInterval(Seconds(.08f));
        }

        sequence.Append(character.DOMove(exit.position, Seconds(movementSeconds)).SetEase(Ease.InCubic))
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

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!orderButton.interactable || balloonRect == null) return;
        balloonFeedback?.Kill();
        balloonFeedback = balloonRect
            .DOScale(balloonBaseScale * balloonHoverScale, Seconds(balloonHoverSeconds))
            .SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (balloonRect == null) return;
        balloonFeedback?.Kill();
        balloonFeedback = balloonRect
            .DOScale(balloonBaseScale, Seconds(balloonHoverSeconds))
            .SetEase(Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!orderButton.interactable || balloonRect == null) return;
        PlayBalloonPop(balloonClickPunch);
    }

    private void PlayBalloonPop(float strength)
    {
        if (balloonRect == null) return;
        balloonFeedback?.Kill();
        balloonRect.localScale = balloonBaseScale;
        balloonFeedback = balloonRect
            .DOPunchScale(Vector3.one * strength, Seconds(.2f), 2, .35f)
            .OnComplete(() => balloonRect.localScale = balloonBaseScale);
    }

    private float Seconds(float duration)
    {
        return owner != null ? owner.AnimationSeconds(duration) : duration;
    }

    private void OnDisable()
    {
        movement?.Kill();
        moodFeedback?.Kill();
        balloonFeedback?.Kill();
        if (character != null) character.localScale = characterBaseScale;
        if (balloonRect != null) balloonRect.localScale = balloonBaseScale;
    }

    private void OnDestroy()
    {
        if (orderButton != null) orderButton.onClick.RemoveListener(SelectOrder);
    }
}
