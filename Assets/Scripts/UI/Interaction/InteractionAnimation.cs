using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reusable interaction animation component for UI elements.
/// Reads animation parameters from an InteractionAnimationConfig ScriptableObject.
/// Provides press/release scale feedback with optional selection confirmation and locked feedback.
/// Exposes events for consumers to react to animation milestones.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public sealed class InteractionAnimation : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [Header("Configuration")]
    [Tooltip("Animation configuration preset. If null, uses sensible defaults.")]
    [SerializeField] private InteractionAnimationConfig config;

    /// <summary>Gets the current animation configuration.</summary>
    public InteractionAnimationConfig Config
    {
        get => config;
        set
        {
            config = value;
            ApplyConfigDefaults();
        }
    }

    [Header("Events")]
    [SerializeField] private bool _eventsFoldout; // Dummy field for Header in Inspector
    [Tooltip("Invoked when press animation starts.")]
    public event Action OnPressStart;

    [Tooltip("Invoked when press animation completes (release/settle).")]
    public event Action OnPressEnd;

    [Tooltip("Invoked when selection confirmation animation plays.")]
    public event Action OnSelectionConfirmed;

    [Tooltip("Invoked when locked feedback animation plays.")]
    public event Action OnLockedAttempt;

    // Cached components
    private RectTransform rectTransform;
    private Selectable selectable;

    // Animation state
    private Vector3 originalScale;
    private Sequence currentSequence;
    private bool isPointerDown;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        originalScale = transform.localScale;
        selectable = GetComponent<Selectable>();
        ApplyConfigDefaults();
    }

    private void OnEnable()
    {
        isPointerDown = false;
        KillSequence();

        if (rectTransform != null)
            rectTransform.localScale = originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanInteract())
            return;

        isPointerDown = true;
        PlayPressAnimation();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPointerDown)
            return;

        isPointerDown = false;
        PlayReleaseAnimation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isPointerDown)
            return;

        isPointerDown = false;
        PlayReleaseAnimation();
    }

    /// <summary>
    /// Plays the selection confirmation animation (pop + punch).
    /// Call this when the item is successfully selected/toggled.
    /// </summary>
    public void PlaySelectionConfirm()
    {
        if (config == null || rectTransform == null)
            return;

        KillSequence();

        float popScale = config.selectionPopScale;
        float popDuration = config.selectionPopDuration;
        float punchAmount = config.selectionPunchAmount;
        float punchDuration = config.selectionPunchDuration;

        var seq = DOTween.Sequence();
        currentSequence = seq;

        // Pop scale
        seq.Append(rectTransform.DOScale(originalScale * popScale, popDuration).SetEase(Ease.OutBack));

        // Punch (small scale oscillation)
        seq.Append(rectTransform.DOPunchScale(Vector3.one * punchAmount, punchDuration, vibrato: 10, elasticity: 0.5f));

        // Settle to original scale
        seq.Append(rectTransform.DOScale(originalScale, 0.1f).SetEase(Ease.OutCubic));

        // Audio & Haptic
        if (config.playConfirmOnRelease)
            AudioManager.Instance?.PlayConfirm();

        HapticFeedback.Play(HapticFeedback.Type.Medium);

        seq.OnComplete(() =>
        {
            currentSequence = null;
            OnSelectionConfirmed?.Invoke();
        });
    }

    /// <summary>
    /// Plays the locked/disabled feedback animation (shake + lock pulse).
    /// Call this when user tries to interact with a locked item.
    /// </summary>
    public void PlayLockedFeedback()
    {
        if (config == null || rectTransform == null)
            return;

        KillSequence();

        float shakeStrength = config.lockedShakeStrength;
        float shakeDuration = config.lockedShakeDuration;
        int shakeVibrato = config.lockedShakeVibrato;

        var seq = DOTween.Sequence();
        currentSequence = seq;

        // Shake position (anchoredPosition)
        seq.Append(rectTransform.DOShakeAnchorPos(shakeDuration, shakeStrength, shakeVibrato, randomness: 90, snapping: false, fadeOut: true));

        // Audio & Haptic
        AudioManager.Instance?.PlayClick(); // or a dedicated error sound
        HapticFeedback.Play(HapticFeedback.Type.Error);

        seq.OnComplete(() =>
        {
            currentSequence = null;
            OnLockedAttempt?.Invoke();
        });
    }

    private bool CanInteract()
    {
        // Dynamic check - selectable might be added/removed at runtime
        selectable = selectable ?? GetComponent<Selectable>();
        return selectable == null || selectable.IsInteractable();
    }

    private void PlayPressAnimation()
    {
        if (config == null || rectTransform == null)
            return;

        KillSequence();

        float targetScale = config.pressScale;
        float duration = config.pressDuration;
        Ease ease = config.pressEase;

        var seq = DOTween.Sequence();
        currentSequence = seq;

        // Apply LateUpdate if configured
        if (config.useLateUpdate)
            seq.SetUpdate(UpdateType.Late, true);

        seq.Append(rectTransform.DOScale(originalScale * targetScale, duration).SetEase(ease));

        // Audio & Haptic on press
        if (config.playClickOnPress)
            AudioManager.Instance?.PlayClick();

        HapticFeedback.Play(HapticFeedback.Type.Light);

        seq.OnStart(() => OnPressStart?.Invoke());
    }

    private void PlayReleaseAnimation()
    {
        if (config == null || rectTransform == null)
            return;

        KillSequence();

        float overshootScale = config.releaseOvershootScale;
        float overshootDuration = config.releaseOvershootDuration;
        Ease overshootEase = config.releaseOvershootEase;
        float settleDuration = config.releaseSettleDuration;
        Ease settleEase = config.releaseSettleEase;

        var seq = DOTween.Sequence();
        currentSequence = seq;

        if (config.useLateUpdate)
            seq.SetUpdate(UpdateType.Late, true);

        // Overshoot
        seq.Append(rectTransform.DOScale(originalScale * overshootScale, overshootDuration).SetEase(overshootEase));

        // Settle to 1.0
        seq.Append(rectTransform.DOScale(originalScale, settleDuration).SetEase(settleEase));

        // Play confirm sound if configured (for primary actions)
        if (config.playConfirmOnRelease)
            AudioManager.Instance?.PlayConfirm();

        seq.OnComplete(() =>
        {
            currentSequence = null;
            OnPressEnd?.Invoke();
        });
    }

    private void KillSequence()
    {
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
            currentSequence = null;
        }
    }

    private void ApplyConfigDefaults()
    {
        if (config == null)
        {
            // Sensible defaults if no config assigned
            config = ScriptableObject.CreateInstance<InteractionAnimationConfig>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rectTransform = rectTransform ?? transform as RectTransform;
        selectable = selectable ?? GetComponent<Selectable>();
        ApplyConfigDefaults();
    }
#endif

    private void OnDisable()
    {
        isPointerDown = false;
        KillSequence();

        if (rectTransform != null)
            rectTransform.localScale = originalScale;
    }

    private void OnDestroy()
    {
        KillSequence();
    }
}