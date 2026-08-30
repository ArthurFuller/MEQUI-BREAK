using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Deve ser anexado a cada botão de opção das grades de cabelo, roupa e acessório.
///
/// Encaminha os cliques ao CustomizationController e controla as animações de
/// pressionar, soltar, confirmar seleção, desbloquear e tentar usar item bloqueado.
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
    [Header("Referências")]
    [SerializeField] private CustomizationController controller;

    [SerializeField, Min(0)]
    private int optionIndex;

    [Header("Visuais de bloqueio (opcional)")]
    [Tooltip("Exibido ou ocultado pelo controlador conforme o estado de bloqueio do item.")]
    [SerializeField] private GameObject lockOverlay;

    [Tooltip("Exibe o preço ou requisito enquanto o item estiver bloqueado.")]
    [SerializeField] private TMP_Text unlockLabel;

    [Header("Configurações de animação")]
    [Tooltip("Multiplicador da escala ao pressionar. Exemplo: 0,92 corresponde a 92% da escala original.")]
    [SerializeField, Range(0.8f, 1f)]
    private float pressScale = 0.92f;

    [Tooltip("Duração da animação de pressionar.")]
    [SerializeField, Min(0.01f)]
    private float pressDuration = 0.08f;

    [Tooltip("Escala excedente ao soltar ou clicar. Exemplo: 1,05 corresponde a 105%.")]
    [SerializeField, Range(1f, 1.2f)]
    private float releaseOvershoot = 1.05f;

    [Tooltip("Duração da animação excedente ao soltar.")]
    [SerializeField, Min(0.01f)]
    private float releaseDuration = 0.12f;

    [Tooltip("Intensidade do pulso de escala ao confirmar uma seleção.")]
    [SerializeField, Min(0f)]
    private float punchIntensity = 0.15f;

    [Tooltip("Duração do pulso ao confirmar uma seleção.")]
    [SerializeField, Min(0.01f)]
    private float punchDuration = 0.3f;

    [Tooltip("Força do balanço ao pressionar um item bloqueado.")]
    [SerializeField, Min(0f)]
    private float shakeStrength = 10f;

    [Tooltip("Duração do balanço do item bloqueado.")]
    [SerializeField, Min(0.01f)]
    private float shakeDuration = 0.3f;

    private Button _button;
    private RectTransform _rectTransform;

    private Vector3 _originalScale;
    private Quaternion _originalRotation;

    private bool _isLocked;

    private Tween _pressTween;
    private Sequence _selectionSequence;
    private Sequence _lockedSequence;

    public int OptionIndex => optionIndex;

    public bool IsLocked => _isLocked;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _rectTransform = GetComponent<RectTransform>();

        _originalScale = _rectTransform.localScale;
        _originalRotation = _rectTransform.localRotation;

        if (controller == null)
            controller = FindFirstObjectByType<CustomizationController>();

        SetLockVisuals(false, string.Empty);
    }

    private void OnDisable()
    {
        KillTweens();
    }

    /// <summary>
    /// Atualiza os visuais para refletir o estado de bloqueio atual.
    /// </summary>
    public void SetLocked(bool isLocked, string label)
    {
        _isLocked = isLocked;
        SetLockVisuals(isLocked, label);

        if (_rectTransform != null)
        {
            KillAnimationSequences();
            _rectTransform.localScale = _originalScale;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanUsePressAnimation())
            return;

        AnimatePress();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!CanUsePressAnimation())
            return;

        AnimateRelease();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!CanUsePressAnimation())
            return;

        KillPressTween();

        if (_rectTransform == null)
            return;

        // Ao sair do botão, cancela o pressionamento sem aplicar o excedente.
        _pressTween = _rectTransform
            .DOScale(_originalScale, releaseDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(UpdateType.Late);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
            return;

        // O controlador revalida o catálogo e o perfil no momento do clique.
        controller?.HandleOptionClicked(optionIndex);
    }

    private bool CanUsePressAnimation()
    {
        return _button != null && _button.interactable && !_isLocked;
    }

    private void AnimatePress()
    {
        KillPressTween();

        _pressTween = _rectTransform
            .DOScale(_originalScale * pressScale, pressDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(UpdateType.Late);
    }

    private void AnimateRelease()
    {
        KillPressTween();

        Sequence releaseSequence = DOTween.Sequence();
        releaseSequence.SetUpdate(UpdateType.Late);
        _pressTween = releaseSequence;

        releaseSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale * releaseOvershoot,
                    releaseDuration * 0.5f
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

        releaseSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale,
                    releaseDuration
                )
                .SetEase(Ease.OutBack)
                .SetUpdate(UpdateType.Late)
        );

        releaseSequence.Play();
    }

    /// <summary>
    /// Reproduz o feedback visual de uma seleção confirmada.
    /// </summary>
    public void AnimateSelectionConfirmed()
    {
        KillAnimationSequences();

        _selectionSequence = DOTween.Sequence();
        _selectionSequence.SetUpdate(UpdateType.Late);

        _selectionSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale * releaseOvershoot,
                    0.06f
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

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

        _selectionSequence.OnComplete(RestoreScale);

        _selectionSequence.Play();
    }

    /// <summary>
    /// Reproduz o balanço de tentativa bloqueada e chama o retorno ao concluir.
    /// </summary>
    public void AnimateLockedFeedback(System.Action onComplete = null)
    {
        // Usa rotação para não disputar anchoredPosition com a animação da grade.
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

    /// <summary>
    /// Reproduz o feedback visual após a compra e desbloqueio do item.
    /// </summary>
    public void AnimateUnlock()
    {
        _isLocked = false;

        KillAnimationSequences();
        SetLockVisuals(false, string.Empty);

        _selectionSequence = DOTween.Sequence();
        _selectionSequence.SetUpdate(UpdateType.Late);

        _selectionSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale * 1.15f,
                    0.15f
                )
                .SetEase(Ease.OutBack)
                .SetUpdate(UpdateType.Late)
        );

        _selectionSequence.Append(
            _rectTransform
                .DOScale(
                    _originalScale,
                    0.1f
                )
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late)
        );

        _selectionSequence.OnComplete(RestoreScale);

        _selectionSequence.Play();
    }

    private void KillPressTween()
    {
        if (_pressTween == null)
            return;

        if (_pressTween.IsActive())
            _pressTween.Kill(true);

        _pressTween = null;
    }

    private void KillAnimationSequences()
    {
        KillPressTween();

        if (_selectionSequence != null)
        {
            if (_selectionSequence.IsActive())
                _selectionSequence.Kill(true);

            _selectionSequence = null;
        }

        if (_lockedSequence != null)
        {
            // Não conclui o balanço interrompido para não antecipar seu callback.
            if (_lockedSequence.IsActive())
                _lockedSequence.Kill(false);

            _lockedSequence = null;
        }

        if (_rectTransform != null)
            _rectTransform.localRotation = _originalRotation;

    }

    private void KillTweens()
    {
        KillAnimationSequences();

        RestoreScale();
    }

    private void SetLockVisuals(bool isLocked, string label)
    {
        if (lockOverlay != null)
            lockOverlay.SetActive(isLocked);

        if (unlockLabel == null)
            return;

        bool showLabel = isLocked && !string.IsNullOrEmpty(label);
        unlockLabel.gameObject.SetActive(showLabel);

        if (showLabel)
            unlockLabel.text = label;
    }

    private void RestoreScale()
    {
        if (_rectTransform != null)
            _rectTransform.localScale = _originalScale;
    }
}
