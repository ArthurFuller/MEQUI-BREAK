using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable panel/popup animator using PanelAnimationConfig.
/// Handles enter/exit animations with slide, fade, and optional CanvasGroup alpha.
/// Call Show() and Hide() to trigger animations.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public sealed class PanelAnimator : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Panel animation configuration preset.")]
    [SerializeField] private PanelAnimationConfig config;

    [Space]
    [Header("References")]
    [Tooltip("CanvasGroup for fade animation. If null, tries to get or add one.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Invoked when enter animation starts.")]
    public event Action OnShowStart;

    [Tooltip("Invoked when enter animation completes.")]
    public event Action OnShowComplete;

    [Tooltip("Invoked when exit animation starts.")]
    public event Action OnHideStart;

    [Tooltip("Invoked when exit animation completes.")]
    public event Action OnHideComplete;

    // Cached components
    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;
    private Sequence currentSequence;
    private bool isShowing;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        // Auto-get or add CanvasGroup for fade
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        isShowing = false;
        KillSequence();
    }

    /// <summary>
    /// Shows the panel with enter animation.
    /// </summary>
    /// <param name="instant">If true, shows instantly without animation.</param>
    public void Show(bool instant = false)
    {
        if (isShowing)
            return;

        isShowing = true;
        KillSequence();

        if (config == null)
        {
            ApplyInstantShow();
            OnShowComplete?.Invoke();
            return;
        }

        if (instant)
        {
            ApplyInstantShow();
            OnShowComplete?.Invoke();
            return;
        }

        PrepareForEnter();

        var seq = DOTween.Sequence();
        currentSequence = seq;

        // LateUpdate for smoother UI animation
        seq.SetUpdate(UpdateType.Late, true);

        OnShowStart?.Invoke();

        // Audio
        if (config.playOpenSound)
            AudioManager.Instance?.PlayClick();

        // Fade in
        if (config.useFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            seq.Join(canvasGroup.DOFade(1f, config.enterDuration).SetEase(config.enterEase));
        }

        // Slide in
        if (config.useSlide && rectTransform != null)
        {
            Vector2 startPos = originalAnchoredPosition + Vector2.down * config.slideOffset;
            rectTransform.anchoredPosition = startPos;
            seq.Join(rectTransform.DOAnchorPos(originalAnchoredPosition, config.enterDuration).SetEase(config.enterEase));
        }
        else if (rectTransform != null)
        {
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }

        seq.OnComplete(() =>
        {
            currentSequence = null;
            isShowing = true;
            OnShowComplete?.Invoke();
        });
    }

    /// <summary>
    /// Hides the panel with exit animation.
    /// </summary>
    /// <param name="instant">If true, hides instantly without animation.</param>
    public void Hide(bool instant = false)
    {
        if (!isShowing && !instant)
            return;

        isShowing = false;
        KillSequence();

        if (config == null || instant)
        {
            ApplyInstantHide();
            OnHideComplete?.Invoke();
            return;
        }

        var seq = DOTween.Sequence();
        currentSequence = seq;

        seq.SetUpdate(UpdateType.Late, true);

        OnHideStart?.Invoke();

        // Audio
        if (config.playCloseSound)
            AudioManager.Instance?.PlayClick();

        // Fade out
        if (config.useFade && canvasGroup != null)
        {
            seq.Join(canvasGroup.DOFade(0f, config.exitDuration).SetEase(config.exitEase));
        }

        // Slide out
        if (config.useSlideOnExit && rectTransform != null)
        {
            Vector2 endPos = originalAnchoredPosition + Vector2.down * config.slideOffset;
            seq.Join(rectTransform.DOAnchorPos(endPos, config.exitDuration).SetEase(config.exitEase));
        }

        seq.OnComplete(() =>
        {
            currentSequence = null;
            isShowing = false;
            ApplyInstantHide();
            OnHideComplete?.Invoke();
        });
    }

    /// <summary>
    /// Toggles panel visibility.
    /// </summary>
    public void Toggle()
    {
        if (isShowing)
            Hide();
        else
            Show();
    }

    /// <summary>
    /// Immediately shows panel without animation (for initialization).
    /// </summary>
    public void ShowInstant()
    {
        Show(true);
    }

    /// <summary>
    /// Immediately hides panel without animation (for initialization).
    /// </summary>
    public void HideInstant()
    {
        Hide(true);
    }

    private void PrepareForEnter()
    {
        if (rectTransform == null)
            return;

        if (config.useSlide)
        {
            Vector2 startPos = originalAnchoredPosition + Vector2.down * config.slideOffset;
            rectTransform.anchoredPosition = startPos;
        }

        if (config.useFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void ApplyInstantShow()
    {
        if (rectTransform != null)
            rectTransform.anchoredPosition = originalAnchoredPosition;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        gameObject.SetActive(true);
    }

    private void ApplyInstantHide()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    private void KillSequence()
    {
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
            currentSequence = null;
        }
    }

    /// <summary>
    /// Updates the config at runtime.
    /// </summary>
    public void SetConfig(PanelAnimationConfig newConfig)
    {
        config = newConfig;
    }

    private void OnDisable()
    {
        KillSequence();
    }

    private void OnDestroy()
    {
        KillSequence();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rectTransform = rectTransform ?? transform as RectTransform;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }
#endif
}