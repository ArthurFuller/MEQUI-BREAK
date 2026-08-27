using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach to each swatch/thumbnail button inside a customization options grid
/// (HairOptions, OutfitOptions or AccessoryOptions).
///
/// Routes clicks through CustomizationController.HandleOptionClicked(optionIndex).
///
/// Includes game feel animations via DOTween:
/// - Press: scale down slightly
/// - Release: scale up with overshoot
/// - Click confirmed: pop + punch
/// - Locked attempt: shake only
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public sealed class AvatarOptionButton : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler,
    IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private CustomizationController controller;

    [SerializeField, Min(0)]
    private int optionIndex;

    [Header("Lock Visuals (Optional)")]
    [Tooltip("Shown/hidden by the controller when this item is locked/unlocked.")]
    [SerializeField] private GameObject lockOverlay;

    [Tooltip("Shows the price or requirement while locked.")]
    [SerializeField] private TMP_Text unlockLabel;

    [Header("Game Feel Animation Settings")]
    [Tooltip("Scale multiplier when pressed (0.92 = 92% of original)")]
    [SerializeField, Range(0.8f, 1f)]
    private float pressScale = 0.92f;

    [Tooltip("Duration of press animation")]
    [SerializeField, Min(0.01f)]
    private float pressDuration = 0.08f;

    [Tooltip("Overshoot scale on release/click (1.05 = 105%)")]
    [SerializeField, Range(1f, 1.2f)]
    private float releaseOvershoot = 1.05f;

    [Tooltip("Duration of release overshoot")]
    [SerializeField, Min(0.01f)]
    private float releaseDuration = 0.12f;

    [Tooltip("Punch scale intensity on confirmed selection")]
    [SerializeField, Min(0f)]
    private float punchIntensity = 0.15f;

    [Tooltip("Punch duration on confirmed selection")]
    [SerializeField, Min(0.01f)]
    private float punchDuration = 0.3f;

    [Tooltip("Shake strength when locked item is pressed")]
    [SerializeField, Min(0f)]
    private float shakeStrength = 10f;

    [Tooltip("Shake duration when locked")]
    [SerializeField, Min(0.01f)]
    private float shakeDuration = 0.3f;

    // Internal state
    private Button _button;
    private RectTransform _rectTransform;

    private Vector3 _originalScale;
    private Quaternion _originalRotation;

    private bool _isLocked;

    private Sequence _pressSequence;
    private Sequence _selectionSequence;
    private Sequence _lockedSequence;

    private Tween _lockPulseTween;

    public int OptionIndex => optionIndex;

    public bool IsLocked => _isLocked;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _rectTransform = GetComponent<RectTransform>();

        _originalScale = _rectTransform.localScale;
        _originalRotation = _rectTransform.localRotation;

        if (controller == null)
        {
            controller = FindFirstObjectByType<CustomizationController>();
        }

        // Ensure lock overlay starts hidden.
        if (lockOverlay != null)
        {
            lockOverlay.SetActive(false);
        }

        // Ensure unlock label starts hidden.
        if (unlockLabel != null)
        {
            unlockLabel.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        // IMPORTANT:
        // This script uses IPointerClickHandler.OnPointerClick().
        // Therefore we do NOT use _button.onClick.AddListener().
        //
        // The previous code had:
        //
        // _button.onClick.AddListener(HandleClick);
        //
        // but HandleClick() does not exist.
        //
        // OnPointerClick() below handles the click directly.
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    /// <summary>
    /// Called by CustomizationController after Start()
    /// and after any purchase to reflect the current unlock state.
    /// </summary>
    public void SetLocked(bool isLocked, string label)
    {
        _isLocked = isLocked;

        if (lockOverlay != null)
        {
            // Preserve the UnlockOverlay alpha exactly as configured in the Inspector.
            // Locked-state refresh only controls visibility; it never changes alpha.
            lockOverlay.SetActive(isLocked);
        }

        if (unlockLabel != null)
        {
            bool showLabel =
                isLocked &&
                !string.IsNullOrEmpty(label);

            unlockLabel.gameObject.SetActive(showLabel);

            if (showLabel)
            {
                unlockLabel.text = label;
            }
        }

        // Reset scale when state changes.
        if (_rectTransform != null)
        {
            KillAnimationSequences();

            _rectTransform.localScale = _originalScale;
        }
    }

    // ============================================================
    // POINTER EVENTS
    // ============================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
            return;

        // Locked items don't get the normal press animation.
        // They use locked feedback on click instead.
        if (_isLocked)
            return;

        AnimatePress();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
            return;

        if (_isLocked)
            return;

        AnimateRelease();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
            return;

        // Locked items never start the normal press animation, so there is
        // nothing to release when the pointer exits.
        if (_isLocked)
            return;

        KillPressTween();

        if (_rectTransform == null)
            return;

        // Pointer exit is a cancelled press, so return using the same release
        // timing as PointerUp, but without the click overshoot. Keep it inside
        // the tracked press sequence so subsequent interactions can kill it
        // cleanly instead of leaving an independent scale tween running.
        _pressSequence = DOTween.Sequence();
        _pressSequence.SetUpdate(UpdateType.Late);

        _pressSequence.Append(
            _rectTransform
                .DOScale(_originalScale, releaseDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

        _pressSequence.Play();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
            return;

        // IMPORTANT: we no longer gate on the cached _isLocked flag here.
        // _isLocked is only refreshed by RefreshLockVisuals() (on scene Start()
        // and right after a purchase), so it can go stale if the player's
        // unlock state (e.g. Level) changes without that refresh running.
        // The click always goes to the controller, which re-checks the catalog
        // + profile live. If it's actually locked, the controller calls back
        // into AnimateLockedFeedback() via HandleLockedOptionClicked().
        controller?.HandleOptionClicked(optionIndex);
    }

    // ============================================================
    // PRESS ANIMATION
    // ============================================================

    private void AnimatePress()
    {
        KillPressTween();

        _pressSequence = DOTween.Sequence();
        _pressSequence.SetUpdate(UpdateType.Late);

        _pressSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale * pressScale,
                    pressDuration
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

        _pressSequence.Play();
    }

    // ============================================================
    // RELEASE ANIMATION
    // ============================================================

    private void AnimateRelease()
    {
        KillPressTween();

        _pressSequence = DOTween.Sequence();
        _pressSequence.SetUpdate(UpdateType.Late);

        // Overshoot.
        _pressSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale * releaseOvershoot,
                    releaseDuration * 0.5f
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

        // Return to normal.
        _pressSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale,
                    releaseDuration
                )
                .SetEase(Ease.OutBack)
                .SetUpdate(UpdateType.Late)
        );

        _pressSequence.Play();
    }

    // ============================================================
    // CONFIRMED SELECTION
    // ============================================================

    /// <summary>
    /// Called by CustomizationController when this option
    /// has been successfully selected.
    /// </summary>
    public void AnimateSelectionConfirmed()
    {
        KillAnimationSequences();

        _selectionSequence = DOTween.Sequence();
        _selectionSequence.SetUpdate(UpdateType.Late);

        // Small pop.
        _selectionSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale * releaseOvershoot,
                    0.06f
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

        // Punch/wobble.
        _selectionSequence.Append(
            _rectTransform
                .DOPunchScale(
                    Vector3.one * punchIntensity,
                    punchDuration,
                    10,
                    0.5f
                )
                .SetUpdate(UpdateType.Late)
        );

        // Guarantee final scale.
        _selectionSequence.OnComplete(() =>
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _originalScale;
            }
        });

        _selectionSequence.Play();
    }

    // ============================================================
    // LOCKED FEEDBACK
    // ============================================================

    /// <summary>
    /// Plays the locked-item shake feedback. Called by CustomizationController
    /// after it confirms (live, against catalog + profile) that this option is
    /// actually locked — see HandleLockedOptionClicked().
    /// </summary>
    public void AnimateLockedFeedback(System.Action onComplete = null)
    {
        // Locked feedback intentionally uses SHAKE only.
        // Use localRotation instead of anchoredPosition because these buttons
        // are children of a GridLayoutGroup and PlayOptionWave also animates
        // anchoredPosition. Shaking position here would make multiple systems
        // compete for the same RectTransform property.
        // Do not fade or pulse the lock overlay/label here.
        KillAnimationSequences();

        if (_rectTransform == null)
        {
            onComplete?.Invoke();
            return;
        }

        _rectTransform.localRotation = _originalRotation;

        _lockedSequence = DOTween.Sequence();
        _lockedSequence.SetUpdate(UpdateType.Late);

        _lockedSequence.Append(
            _rectTransform
                .DOShakeRotation(
                    shakeDuration,
                    new Vector3(0f, 0f, shakeStrength),
                    20,
                    90f,
                    true
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

        _lockedSequence.OnComplete(() =>
        {
            if (_rectTransform != null)
                _rectTransform.localRotation = _originalRotation;

            onComplete?.Invoke();
        });
        _lockedSequence.Play();
    }

    // ============================================================
    // UNLOCK ANIMATION
    // ============================================================

    /// <summary>
    /// Called when this item gets unlocked after purchase.
    /// </summary>
    public void AnimateUnlock()
    {
        _isLocked = false;

        KillAnimationSequences();

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(UpdateType.Late);

        // UnlockOverlay/UnlockLabel alpha is intentionally never modified.
        // When the item becomes unlocked, simply hide the lock visuals and preserve
        // their Inspector alpha (e.g. 0.8) for any future activation.
        if (lockOverlay != null)
        {
            lockOverlay.SetActive(false);
        }

        if (unlockLabel != null)
        {
            unlockLabel.gameObject.SetActive(false);
        }

        // Pop after unlocking.
        seq.Append(
            _rectTransform
                .DOScale(
                    _originalScale * 1.15f,
                    0.15f
                )
                .SetEase(Ease.OutBack)
                .SetUpdate(UpdateType.Late)
        );

        seq.Append(
            _rectTransform
                .DOScale(
                    _originalScale,
                    0.1f
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

        seq.OnComplete(() =>
        {
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _originalScale;
            }
        });

        seq.Play();
    }

    // ============================================================
    // CLEANUP
    // ============================================================

    private void KillPressTween()
    {
        if (_pressSequence != null &&
            _pressSequence.IsActive())
        {
            _pressSequence.Kill(true);
            _pressSequence = null;
        }
    }

    private void KillAnimationSequences()
    {
        KillPressTween();

        if (_selectionSequence != null &&
            _selectionSequence.IsActive())
        {
            _selectionSequence.Kill(true);
            _selectionSequence = null;
        }

        if (_lockedSequence != null &&
            _lockedSequence.IsActive())
        {
            // Do not complete an interrupted locked shake: completing it would
            // invoke its popup callback early. A new click should restart the
            // shake and produce only one popup when that shake finishes.
            _lockedSequence.Kill(false);
            _lockedSequence = null;
        }

        if (_rectTransform != null)
            _rectTransform.localRotation = _originalRotation;

        _lockPulseTween?.Kill(true);
        _lockPulseTween = null;
    }

    private void KillTweens()
    {
        KillAnimationSequences();

        // Always return to original scale.
        if (_rectTransform != null)
        {
            _rectTransform.localScale = _originalScale;
        }
    }

    // ============================================================
    // HELPERS
    // ============================================================

    // ============================================================
    // RUNTIME SETTERS
    // ============================================================

    /// <summary>
    /// Sets the option index at runtime.
    /// Useful for dynamically generated grids.
    /// </summary>
    public void SetOptionIndex(int index)
    {
        optionIndex = index;
    }

    /// <summary>
    /// Sets the controller reference at runtime.
    /// </summary>
    public void SetController(
        CustomizationController newController)
    {
        controller = newController;
    }
}