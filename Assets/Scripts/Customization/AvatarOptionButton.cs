using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Deve ser anexado a cada botão das grades de chapéu, rosto e cor.
///
/// Encaminha os cliques ao CustomizationController e reproduz um único pulso
/// simples para seleção ou tentativa de usar um item bloqueado.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public sealed class AvatarOptionButton : MonoBehaviour,
    IPointerClickHandler
{
    [Header("Referências")]
    [SerializeField] private CustomizationController controller;

    [Tooltip("Cor do fundo quando esta opção está selecionada.")]
    [SerializeField] private Color selectedBackgroundColor = new Color(0.24f, 0.20f, 0.04f, 1f);

    [Tooltip("Cor clara usada por todos os cards disponíveis.")]
    [SerializeField] private Color unlockedBackgroundColor = new Color(0.38f, 0.38f, 0.38f, 1f);

    [Tooltip("Cor usada enquanto o item ainda está bloqueado.")]
    [SerializeField] private Color lockedBackgroundColor = new Color(0.27f, 0.27f, 0.27f, 1f);

    [SerializeField, Min(0)]
    private int optionIndex;

    [Header("Visuais de bloqueio (opcional)")]
    [Tooltip("Exibido ou ocultado pelo controlador conforme o estado de bloqueio do item.")]
    [SerializeField] private GameObject lockOverlay;

    [Tooltip("Exibe o preço ou requisito enquanto o item estiver bloqueado.")]
    [SerializeField] private TMP_Text unlockLabel;

    [Header("Configurações de animação")]
    [Tooltip("Aumento máximo do pulso único ao clicar.")]
    [SerializeField, Range(1f, 1.1f)] private float pulseScale = 1.04f;

    [Tooltip("Duração de cada metade do pulso.")]
    [SerializeField, Min(0.01f)] private float pulseHalfDuration = 0.1f;

    private Button _button;
    private Image _backgroundImage;
    private RectTransform _rectTransform;

    private Vector3 _originalScale;
    private Quaternion _originalRotation;
    private bool _isLocked;
    private bool _isSelected;
    private bool _referencesCached;
    private bool _lockStateInitialized;

    private Sequence _selectionSequence;

    public int OptionIndex => optionIndex;

    public bool IsLocked => _isLocked;

    private void Awake()
    {
        CacheReferences();

        if (!_lockStateInitialized)
            SetLockVisuals(false, string.Empty);
    }

    private void CacheReferences()
    {
        if (_referencesCached)
            return;

        _button = GetComponent<Button>();
        _backgroundImage = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();

        _originalScale = _rectTransform.localScale;
        _originalRotation = _rectTransform.localRotation;
        _referencesCached = true;
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
        CacheReferences();
        _isLocked = isLocked;
        _lockStateInitialized = true;
        SetLockVisuals(isLocked, label);

        if (_rectTransform != null)
        {
            KillAnimationSequences();
            _rectTransform.localScale = _originalScale;
        }
    }

    /// <summary>Destaca a opção salva ou selecionada sem criar elementos em runtime.</summary>
    public void SetSelected(bool isSelected)
    {
        CacheReferences();
        _isSelected = isSelected;
        ApplyBackgroundColor();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable)
            return;

        // O controlador revalida o catálogo e o perfil no momento do clique.
        controller?.HandleOptionClicked(optionIndex);
    }

    /// <summary>
    /// Reproduz o feedback visual de uma seleção confirmada.
    /// </summary>
    public void AnimateSelectionConfirmed()
    {
        PlaySinglePulse();
    }

    /// <summary>
    /// Reproduz o mesmo pulso simples na tentativa bloqueada e chama o retorno ao concluir.
    /// </summary>
    public void AnimateLockedFeedback(System.Action onComplete = null)
    {
        PlaySinglePulse(onComplete);
    }

    /// <summary>
    /// Reproduz o feedback visual após a compra e desbloqueio do item.
    /// </summary>
    public void AnimateUnlock()
    {
        _isLocked = false;
        SetLockVisuals(false, string.Empty);
    }

    private void PlaySinglePulse(System.Action onComplete = null)
    {
        KillAnimationSequences();

        if (_rectTransform == null)
        {
            onComplete?.Invoke();
            return;
        }

        _selectionSequence = DOTween.Sequence();
        _selectionSequence
            .SetUpdate(UpdateType.Late)
            .Append(_rectTransform
                .DOScale(_originalScale * pulseScale, pulseHalfDuration)
                .SetEase(Ease.OutSine)
                .SetUpdate(UpdateType.Late))
            .Append(_rectTransform
                .DOScale(_originalScale, pulseHalfDuration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(UpdateType.Late))
            .OnComplete(() =>
            {
                RestoreScale();
                onComplete?.Invoke();
            });
    }

    private void KillAnimationSequences()
    {
        if (_selectionSequence != null)
        {
            if (_selectionSequence.IsActive())
                _selectionSequence.Kill(false);

            _selectionSequence = null;
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
        ApplyBackgroundColor();

        if (lockOverlay != null)
            lockOverlay.SetActive(isLocked);

        if (unlockLabel == null)
            return;

        bool showLabel = isLocked && !string.IsNullOrEmpty(label);
        unlockLabel.gameObject.SetActive(showLabel);

        if (showLabel)
            unlockLabel.text = label;
    }

    private void ApplyBackgroundColor()
    {
        if (_backgroundImage == null)
            return;

        _backgroundImage.color = _isSelected && !_isLocked
            ? selectedBackgroundColor
            : (_isLocked ? lockedBackgroundColor : unlockedBackgroundColor);
    }

    private void RestoreScale()
    {
        if (_rectTransform != null)
            _rectTransform.localScale = _originalScale;
    }
}
