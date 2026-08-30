using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mobile-friendly held state for a Slider handle. It only reacts while the user
/// is touching/dragging the control and never changes the slider value itself.
/// </summary>
[RequireComponent(typeof(Slider))]
[DisallowMultipleComponent]
public sealed class UISliderTouchFeedback : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler,
    IBeginDragHandler,
    IEndDragHandler
{
    [SerializeField, Range(1f, 1.2f)] private float heldScale = 1.10f;
    [SerializeField, Min(0.03f)] private float enterDuration = 0.10f;
    [SerializeField, Min(0.03f)] private float exitDuration = 0.12f;

    private Slider slider;
    private RectTransform handle;
    private Vector3 baseScale;
    private Tween scaleTween;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        handle = slider.handleRect;
        if (handle != null)
            baseScale = handle.localScale;
    }

    public void OnPointerDown(PointerEventData eventData) => SetHeld(true);
    public void OnBeginDrag(PointerEventData eventData) => SetHeld(true);
    public void OnPointerUp(PointerEventData eventData) => SetHeld(false);
    public void OnEndDrag(PointerEventData eventData) => SetHeld(false);
    public void OnPointerExit(PointerEventData eventData) => SetHeld(false);

    private void SetHeld(bool held)
    {
        if (slider == null || !slider.interactable || handle == null)
            return;

        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill(false);

        Vector3 target = held ? baseScale * heldScale : baseScale;
        float duration = held ? enterDuration : exitDuration;

        scaleTween = handle.DOScale(target, duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(UIMotionDefaults.UseUnscaledTime)
            .OnComplete(() => scaleTween = null);
    }

    private void OnDisable()
    {
        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill(false);
        scaleTween = null;

        if (handle != null)
            handle.localScale = baseScale;
    }
}
