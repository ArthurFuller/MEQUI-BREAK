using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResultPopup : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private Image overlayImage;
    [SerializeField, Range(0f, 1f)] private float overlayMaxAlpha = 0.7f;

    [Header("Panel")]
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text pointsLabel;
    [SerializeField] private Button continueButton;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float enterDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float exitDuration = 0.35f;
    [SerializeField, Min(0f)] private float slideOffset = 800f;

    [Header("Navigation")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private string hubSceneName = "Hub";

    private Vector2 _shownPos;
    private Vector2 _hiddenPos;
    private Sequence _activeSequence;

    private void Awake()
    {
        AssertReferences();

        _shownPos = popupPanel.anchoredPosition;
        _hiddenPos = _shownPos + new Vector2(0f, -slideOffset);

        continueButton.onClick.AddListener(PlayOut);

        HideInstant();
    }

    public void Show(int pointsEarned)
    {
        titleLabel.text = "Minigame Concluído";
        pointsLabel.text = $"+{pointsEarned} Break Points";

        ResetVisualState();
        gameObject.SetActive(true);
        PlayIn();
    }

    private void PlayIn()
    {
        KillSequence();

        popupCanvasGroup.blocksRaycasts = true;

        _activeSequence = DOTween.Sequence()
            .Join(overlayImage.DOFade(overlayMaxAlpha, enterDuration * 0.6f).SetEase(Ease.OutQuad))
            .Join(popupCanvasGroup.DOFade(1f, enterDuration).SetEase(Ease.OutQuad))
            .Join(popupPanel.DOAnchorPos(_shownPos, enterDuration).SetEase(Ease.OutCubic));
    }

    private void PlayOut()
    {
        popupCanvasGroup.blocksRaycasts = false;

        KillSequence();

        _activeSequence = DOTween.Sequence()
            .Join(overlayImage.DOFade(0f, exitDuration).SetEase(Ease.InQuad))
            .Join(popupCanvasGroup.DOFade(0f, exitDuration).SetEase(Ease.InQuad))
            .Join(popupPanel.DOAnchorPos(_hiddenPos, exitDuration).SetEase(Ease.InCubic))
            .OnComplete(GoToHub);
    }

    private void GoToHub()
    {
        sceneLoader.Load(hubSceneName);
    }

    private void ResetVisualState()
    {
        Color overlayColor = overlayImage.color;
        overlayColor.a = 0f;
        overlayImage.color = overlayColor;

        popupCanvasGroup.alpha = 0f;
        popupPanel.anchoredPosition = _hiddenPos;
    }

    private void HideInstant()
    {
        ResetVisualState();
        popupCanvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    private void KillSequence()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
            _activeSequence.Kill();

        _activeSequence = null;
    }

    private void OnDisable()
    {
        KillSequence();
    }

    private void AssertReferences()
    {
        if (overlayImage == null)
            Debug.LogError($"{name}: overlayImage não atribuído.", this);

        if (popupPanel == null)
            Debug.LogError($"{name}: popupPanel não atribuído.", this);

        if (popupCanvasGroup == null)
            Debug.LogError($"{name}: popupCanvasGroup não atribuído.", this);

        if (titleLabel == null)
            Debug.LogError($"{name}: titleLabel não atribuído.", this);

        if (pointsLabel == null)
            Debug.LogError($"{name}: pointsLabel não atribuído.", this);

        if (continueButton == null)
            Debug.LogError($"{name}: continueButton não atribuído.", this);

        if (sceneLoader == null)
            Debug.LogError($"{name}: sceneLoader não atribuído.", this);
    }
}