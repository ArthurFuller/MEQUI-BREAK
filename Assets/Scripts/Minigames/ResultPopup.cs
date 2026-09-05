using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResultPopup : MonoBehaviour
{
    [Header("Sobreposição")]
    [SerializeField] private Image overlayImage;
    [SerializeField, Range(0f, 1f)] private float overlayMaxAlpha = 0.7f;

    [Header("Painel")]
    [SerializeField] private RectTransform popupPanel;
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text pointsLabel;
    [SerializeField] private Button continueButton;

    [Header("Animação")]
    [SerializeField, Min(0.05f)] private float enterDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float exitDuration = 0.35f;
    [SerializeField, Min(0f)] private float slideOffset = 800f;

    [Header("Contador de recompensa")]
    [Tooltip("Pequena pausa antes de iniciar a contagem dos PB recebidos.")]
    [SerializeField, Min(0f)] private float pointsCountDelay = 0.12f;

    [Tooltip("Duração da contagem de +0 até o total de PB recebido.")]
    [SerializeField, Min(0.05f)] private float pointsCountDuration = 0.65f;

    [SerializeField] private Ease pointsCountEase = Ease.OutCubic;

    [Header("Navegação")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private string hubSceneName = "Hub";

    private Vector2 _shownPos;
    private Vector2 _hiddenPos;
    private Sequence _activeSequence;
    private Sequence _pointsSequence;
    private int _displayedRewardPoints;
    private int _lastRewardLabelValue = int.MinValue;

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
        _displayedRewardPoints = 0;
        _lastRewardLabelValue = int.MinValue;
        UpdateRewardLabel(0);

        ResetVisualState();
        gameObject.SetActive(true);
        PlayIn();
        PlayRewardCounter(pointsEarned);
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
        KillPointsSequence(complete: true);

        _activeSequence = DOTween.Sequence()
            .Join(overlayImage.DOFade(0f, exitDuration).SetEase(Ease.InQuad))
            .Join(popupCanvasGroup.DOFade(0f, exitDuration).SetEase(Ease.InQuad))
            .Join(popupPanel.DOAnchorPos(_hiddenPos, exitDuration).SetEase(Ease.InCubic))
            .OnComplete(GoToHub);
    }

    private void PlayRewardCounter(int pointsEarned)
    {
        KillPointsSequence(complete: false);

        int target = Mathf.Max(0, pointsEarned);
        _displayedRewardPoints = 0;
        UpdateRewardLabel(0);

        _pointsSequence = DOTween.Sequence();

        if (pointsCountDelay > 0f)
            _pointsSequence.AppendInterval(pointsCountDelay);

        if (target > 0)
        {
            Tween countTween = DOTween.To(
                () => _displayedRewardPoints,
                value =>
                {
                    _displayedRewardPoints = value;
                    UpdateRewardLabel(value);
                },
                target,
                pointsCountDuration
            ).SetEase(pointsCountEase);

            _pointsSequence.Append(countTween);
        }

        _pointsSequence.AppendCallback(() =>
        {
            _displayedRewardPoints = target;
            UpdateRewardLabel(target);
        });
    }

    private void UpdateRewardLabel(int value)
    {
        if (pointsLabel == null || value == _lastRewardLabelValue)
            return;

        _lastRewardLabelValue = value;
        pointsLabel.SetText("+{0} Break Points", value);
    }

    private void KillPointsSequence(bool complete)
    {
        if (_pointsSequence != null && _pointsSequence.IsActive())
            _pointsSequence.Kill(complete);

        _pointsSequence = null;
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
        KillPointsSequence(complete: false);
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(PlayOut);
    }

    private void AssertReferences()
    {
        AssertReference(overlayImage, nameof(overlayImage));
        AssertReference(popupPanel, nameof(popupPanel));
        AssertReference(popupCanvasGroup, nameof(popupCanvasGroup));
        AssertReference(titleLabel, nameof(titleLabel));
        AssertReference(pointsLabel, nameof(pointsLabel));
        AssertReference(continueButton, nameof(continueButton));
        AssertReference(sceneLoader, nameof(sceneLoader));
    }

    private void AssertReference(Object reference, string fieldName)
    {
        if (reference == null)
            Debug.LogError($"{name}: {fieldName} não atribuído.", this);
    }
}
