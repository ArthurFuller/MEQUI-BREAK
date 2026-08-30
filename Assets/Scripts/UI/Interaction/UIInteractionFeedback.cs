using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Fornece um pequeno feedback reutilizável de pressionar e soltar para elementos interativos.
/// Atua somente sobre a escala e não interfere no comportamento funcional do elemento.
/// </summary>
public sealed class UIInteractionFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Feedback de pressão")]
    [SerializeField, Range(0.85f, 1f)] private float pressScale = 0.96f;
    [SerializeField, Min(0f)] private float pressDuration = 0.07f;
    [SerializeField, Min(0f)] private float releaseDuration = 0.10f;
    [SerializeField] private Ease pressEase = Ease.OutQuad;
    [SerializeField] private Ease releaseEase = Ease.OutQuad;

    private RectTransform rectTransform;
    private Selectable selectable;
    private Vector3 originalScale;
    private Tween scaleTween;
    private bool isPointerDown;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        selectable = GetComponent<Selectable>();
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        isPointerDown = false;
        KillScaleTween();

        if (rectTransform != null)
            rectTransform.localScale = originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanInteract())
            return;

        isPointerDown = true;
        AnimateScale(originalScale * pressScale, pressDuration, pressEase);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleasePointer();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ReleasePointer();
    }

    private void ReleasePointer()
    {
        if (!isPointerDown)
            return;

        isPointerDown = false;
        AnimateScale(originalScale, releaseDuration, releaseEase);
    }

    private bool CanInteract()
    {
        return selectable == null || selectable.IsInteractable();
    }

    private void AnimateScale(Vector3 targetScale, float duration, Ease ease)
    {
        KillScaleTween();

        if (rectTransform == null)
            return;

        if (duration <= 0f)
        {
            rectTransform.localScale = targetScale;
            return;
        }

        scaleTween = rectTransform.DOScale(targetScale, duration).SetEase(ease);
    }

    private void KillScaleTween()
    {
        if (scaleTween == null)
            return;

        scaleTween.Kill();
        scaleTween = null;
    }

    private void OnDisable()
    {
        isPointerDown = false;
        KillScaleTween();

        if (rectTransform != null)
            rectTransform.localScale = originalScale;
    }

    private void OnDestroy()
    {
        KillScaleTween();
    }
}
