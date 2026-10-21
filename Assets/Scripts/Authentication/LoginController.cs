using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Controla o cadastro inicial de Nome, Loja e Turno.
/// O nome público Login é preservado para manter o UnityEvent da cena.
/// </summary>
public sealed class LoginController : MonoBehaviour
{
    [Header("Navegação")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private string hubScene = "HUB";

    [Header("Campos do cadastro")]
    [FormerlySerializedAs("employeeIdInput")]
    [SerializeField] private TMP_InputField nameInput;

    [FormerlySerializedAs("passwordInput")]
    [SerializeField] private TMP_InputField storeInput;

    [Header("Seleção de turno")]
    [SerializeField] private Button shiftSelectorButton;
    [SerializeField] private Image shiftSelectorFrame;
    [SerializeField] private TMP_Text shiftValueText;
    [SerializeField] private RectTransform shiftOptionsPanel;
    [SerializeField] private CanvasGroup shiftOptionsCanvasGroup;
    [SerializeField] private Image shiftPanelBackground;
    [SerializeField] private CanvasGroup morningOptionGroup;
    [SerializeField] private CanvasGroup afternoonOptionGroup;
    [SerializeField] private CanvasGroup nightOptionGroup;

    [Header("Game feel do turno")]
    [SerializeField] private string shiftPlaceholder = "Selecione o turno";
    [SerializeField] private Color shiftPlaceholderColor = new Color(0.275f, 0.275f, 0.275f, 1f);
    [SerializeField] private Color shiftSelectedColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField, Min(0.05f)] private float shiftPanelDuration = 0.3f;
    [SerializeField] private Ease shiftPanelEase = Ease.InOutSine;
    [SerializeField, Min(0.03f)] private float shiftOptionDuration = 0.28f;
    [SerializeField, Range(0f, 0.15f)] private float shiftOptionStagger = 0.04f;
    [SerializeField, Range(0f, 24f)] private float shiftOptionStartOffset = 12f;
    [SerializeField] private Ease shiftOptionEase = Ease.InOutSine;
    [SerializeField, Range(0f, 0.12f)] private float selectionPunchScale = 0.05f;

    [Header("Interface")]
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text errorText;

    private bool isSubmitting;
    private bool shiftOptionsOpen;
    private string selectedShift = string.Empty;
    private Tween shiftPanelTween;
    private Tween shiftSelectionTween;
    private Tween shiftFrameTween;
    private float shiftPanelBackgroundAlpha = 1f;
    private Color shiftFrameColor;
    private Vector2 shiftPanelExpandedSize;
    private Vector2 morningOptionPosition;
    private Vector2 afternoonOptionPosition;
    private Vector2 nightOptionPosition;

    private void Awake()
    {
        ConfigureInput(nameInput, PlayerManager.DisplayNameMaxLength, "Digite seu nome completo");
        ConfigureInput(storeInput, PlayerManager.StoreIdMaxLength, "Número ou nome da filial");

        if (shiftPanelBackground != null)
            shiftPanelBackgroundAlpha = shiftPanelBackground.color.a;
        if (shiftOptionsPanel != null)
        {
            shiftPanelExpandedSize = shiftOptionsPanel.sizeDelta;
            if (shiftOptionsPanel.GetComponent<RectMask2D>() == null)
                shiftOptionsPanel.gameObject.AddComponent<RectMask2D>();
        }
        if (shiftSelectorFrame != null)
            shiftFrameColor = shiftSelectorFrame.color;
        morningOptionPosition = OptionPosition(morningOptionGroup);
        afternoonOptionPosition = OptionPosition(afternoonOptionGroup);
        nightOptionPosition = OptionPosition(nightOptionGroup);

        RaiseCaret(nameInput);
        RaiseCaret(storeInput);

        selectedShift = string.Empty;
        UpdateShiftLabel();
        CloseShiftOptions(immediate: true);
    }

    private void OnEnable()
    {
        SetListeners(true);
        ClearError();
        CloseShiftOptions(immediate: true);
        UpdateSubmitState();
    }

    private void OnDisable()
    {
        SetListeners(false);
        KillShiftTweens();
    }

    /// <summary>
    /// Valida, salva o cadastro e carrega o HUB uma única vez.
    /// </summary>
    public void Login()
    {
        if (isSubmitting)
            return;

        var player = PlayerManager.Instance;
        if (player == null)
        {
            SetError("Não foi possível acessar os dados do usuário.");
            return;
        }

        if (sceneLoader == null)
        {
            SetError("Não foi possível abrir a próxima tela.");
            return;
        }

        string displayName = nameInput != null ? nameInput.text : string.Empty;
        string storeId = storeInput != null ? storeInput.text : string.Empty;
        string shift = selectedShift;

        if (!player.TryCompleteRegistration(displayName, storeId, shift, out string error))
        {
            SetError(error);
            UpdateSubmitState();
            return;
        }

        isSubmitting = true;
        SetInputsInteractable(false);
        ClearError();
        UnityEngine.Object.FindFirstObjectByType<LocalStorage>()?.GetAnonymousParticipantId();
        AudioManager.Instance?.PlayLoginJingle();
        sceneLoader.Load(hubScene);
    }

    private static void ConfigureInput(TMP_InputField input, int characterLimit, string placeholder)
    {
        if (input == null)
            return;

        input.contentType = TMP_InputField.ContentType.Standard;
        input.inputType = TMP_InputField.InputType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.shouldHideMobileInput = true;
        input.characterLimit = characterLimit;

        if (input.placeholder is TMP_Text placeholderText)
            placeholderText.text = placeholder;
    }

    private void SetListeners(bool enabled)
    {
        SetInputListeners(nameInput, enabled);
        SetInputListeners(storeInput, enabled);

        if (nameInput != null)
        {
            if (enabled)
            {
                nameInput.onSubmit.AddListener(FocusStore);
                nameInput.onSelect.AddListener(FocusNameCaret);
            }
            else
            {
                nameInput.onSubmit.RemoveListener(FocusStore);
                nameInput.onSelect.RemoveListener(FocusNameCaret);
            }
        }

        if (storeInput != null)
        {
            if (enabled)
            {
                storeInput.onSubmit.AddListener(FocusShift);
                storeInput.onSelect.AddListener(FocusStoreCaret);
            }
            else
            {
                storeInput.onSubmit.RemoveListener(FocusShift);
                storeInput.onSelect.RemoveListener(FocusStoreCaret);
            }
        }

    }

    private void SetInputListeners(TMP_InputField input, bool enabled)
    {
        if (input == null)
            return;

        if (enabled) input.onValueChanged.AddListener(HandleValueChanged);
        else input.onValueChanged.RemoveListener(HandleValueChanged);
    }

    private void HandleValueChanged(string _)
    {
        ClearError();
        UpdateSubmitState();
    }

    private void FocusStore(string _) => storeInput?.ActivateInputField();

    private void FocusNameCaret(string _) => RaiseCaret(nameInput);

    private void FocusStoreCaret(string _) => RaiseCaret(storeInput);

    private static void RaiseCaret(TMP_InputField input)
    {
        if (input == null || input.textViewport == null) return;
        Transform caret = input.textViewport.Find("Caret");
        if (caret != null) caret.SetAsLastSibling();
    }

    private void FocusShift(string _)
    {
        FlashShiftFrame();
        OpenShiftOptions();
    }

    private void UpdateSubmitState()
    {
        if (submitButton == null || isSubmitting)
            return;

        bool isValid = PlayerManager.TryValidateRegistration(
            nameInput != null ? nameInput.text : string.Empty,
            storeInput != null ? storeInput.text : string.Empty,
            selectedShift,
            out _);

        if (submitButton.interactable != isValid)
            submitButton.interactable = isValid;
    }

    private void SetInputsInteractable(bool interactable)
    {
        if (nameInput != null) nameInput.interactable = interactable;
        if (storeInput != null) storeInput.interactable = interactable;
        if (shiftSelectorButton != null) shiftSelectorButton.interactable = interactable;
        if (submitButton != null) submitButton.interactable = interactable;
    }

    public void ToggleShiftOptions()
    {
        if (shiftOptionsPanel == null || isSubmitting)
            return;

        FlashShiftFrame();
        if (shiftOptionsOpen)
            CloseShiftOptions(immediate: false);
        else
            OpenShiftOptions();
    }

    private void OpenShiftOptions()
    {
        if (shiftOptionsPanel == null || shiftOptionsCanvasGroup == null || isSubmitting)
            return;

        bool wasVisible = shiftOptionsPanel.gameObject.activeSelf;
        KillPanelTween();
        shiftOptionsOpen = true;
        shiftOptionsPanel.gameObject.SetActive(true);
        shiftOptionsPanel.localScale = Vector3.one;
        if (!wasVisible)
            shiftOptionsPanel.sizeDelta = new Vector2(shiftPanelExpandedSize.x, 0f);
        shiftOptionsCanvasGroup.alpha = 1f;
        shiftOptionsCanvasGroup.interactable = true;
        shiftOptionsCanvasGroup.blocksRaycasts = true;

        SetBackgroundAlpha(shiftPanelBackgroundAlpha);
        if (!wasVisible)
        {
            PrepareOptionForReveal(morningOptionGroup, morningOptionPosition);
            PrepareOptionForReveal(afternoonOptionGroup, afternoonOptionPosition);
            PrepareOptionForReveal(nightOptionGroup, nightOptionPosition);
        }

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(shiftOptionsPanel.DOSizeDelta(shiftPanelExpandedSize, shiftPanelDuration)
            .SetEase(shiftPanelEase));

        InsertOptionReveal(sequence, morningOptionGroup, morningOptionPosition, 0f);
        InsertOptionReveal(sequence, afternoonOptionGroup, afternoonOptionPosition, shiftOptionStagger);
        InsertOptionReveal(sequence, nightOptionGroup, nightOptionPosition, shiftOptionStagger * 2f);

        shiftPanelTween = sequence;
    }

    private void CloseShiftOptions(bool immediate)
    {
        if (shiftOptionsPanel == null || shiftOptionsCanvasGroup == null)
            return;

        KillPanelTween();
        shiftOptionsOpen = false;
        shiftOptionsCanvasGroup.blocksRaycasts = false;

        if (immediate || !shiftOptionsPanel.gameObject.activeSelf)
        {
            ResetClosedShiftPanel();
            return;
        }

        float closeDuration = shiftPanelDuration * 0.6f;
        float reverseStagger = shiftOptionStagger * 0.5f;
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(shiftOptionsPanel.DOSizeDelta(
            new Vector2(shiftPanelExpandedSize.x, 0f), closeDuration).SetEase(Ease.InCubic));
        InsertOptionHide(sequence, nightOptionGroup, nightOptionPosition, 0f, closeDuration);
        InsertOptionHide(sequence, afternoonOptionGroup, afternoonOptionPosition,
            reverseStagger, closeDuration - reverseStagger);
        InsertOptionHide(sequence, morningOptionGroup, morningOptionPosition,
            reverseStagger * 2f, closeDuration - reverseStagger * 2f);
        shiftPanelTween = sequence.OnComplete(ResetClosedShiftPanel);
    }

    private static Vector2 OptionPosition(CanvasGroup optionGroup) =>
        optionGroup != null ? ((RectTransform)optionGroup.transform).anchoredPosition : Vector2.zero;

    private void PrepareOptionForReveal(CanvasGroup optionGroup, Vector2 position)
    {
        if (optionGroup == null)
            return;

        optionGroup.alpha = 1f;
        ((RectTransform)optionGroup.transform).anchoredPosition = position + Vector2.down * shiftOptionStartOffset;
    }

    private void InsertOptionReveal(Sequence sequence, CanvasGroup optionGroup, Vector2 position, float atPosition)
    {
        if (optionGroup == null)
            return;

        sequence.Insert(atPosition,
            ((RectTransform)optionGroup.transform).DOAnchorPos(position, shiftOptionDuration)
                .SetEase(shiftOptionEase));
    }

    private void InsertOptionHide(Sequence sequence, CanvasGroup optionGroup, Vector2 position,
        float atPosition, float duration)
    {
        if (optionGroup == null) return;
        sequence.Insert(atPosition, ((RectTransform)optionGroup.transform)
            .DOAnchorPos(position + Vector2.down * shiftOptionStartOffset,
                Mathf.Max(.01f, duration)).SetEase(Ease.InCubic));
    }

    private void ResetClosedShiftPanel()
    {
        shiftOptionsCanvasGroup.alpha = 1f;
        shiftOptionsCanvasGroup.interactable = true;
        shiftOptionsCanvasGroup.blocksRaycasts = false;
        shiftOptionsPanel.localScale = Vector3.one;
        shiftOptionsPanel.sizeDelta = shiftPanelExpandedSize;
        SetBackgroundAlpha(shiftPanelBackgroundAlpha);
        ResetOptionVisual(morningOptionGroup, morningOptionPosition);
        ResetOptionVisual(afternoonOptionGroup, afternoonOptionPosition);
        ResetOptionVisual(nightOptionGroup, nightOptionPosition);
        shiftOptionsPanel.gameObject.SetActive(false);
    }

    private static void ResetOptionVisual(CanvasGroup optionGroup, Vector2 position)
    {
        if (optionGroup == null)
            return;

        optionGroup.alpha = 1f;
        ((RectTransform)optionGroup.transform).anchoredPosition = position;
    }

    private void SetBackgroundAlpha(float alpha)
    {
        if (shiftPanelBackground == null)
            return;

        Color color = shiftPanelBackground.color;
        color.a = alpha;
        shiftPanelBackground.color = color;
    }

    private void FlashShiftFrame()
    {
        if (shiftSelectorFrame == null) return;
        shiftFrameTween?.Kill();
        shiftSelectorFrame.color = shiftFrameColor;
        shiftFrameTween = shiftSelectorFrame
            .DOColor(new Color(.784f, .784f, .784f, shiftFrameColor.a), .1f)
            .SetLoops(2, LoopType.Yoyo).SetUpdate(true)
            .OnComplete(() => shiftSelectorFrame.color = shiftFrameColor);
    }

    public void SelectMorning() => SelectShift("Manhã");

    public void SelectAfternoon() => SelectShift("Tarde");

    public void SelectNight() => SelectShift("Noite");

    private void SelectShift(string value)
    {
        selectedShift = value;
        UpdateShiftLabel();
        ClearError();
        UpdateSubmitState();
        CloseShiftOptions(immediate: false);

        if (shiftValueText == null)
            return;

        if (shiftSelectionTween != null && shiftSelectionTween.IsActive())
            shiftSelectionTween.Kill();

        RectTransform labelRect = shiftValueText.rectTransform;
        labelRect.localScale = Vector3.one;
        shiftSelectionTween = labelRect
            .DOPunchScale(new Vector3(selectionPunchScale, selectionPunchScale, 0f), 0.22f, 5, 0.45f)
            .SetUpdate(true);
    }

    private void UpdateShiftLabel()
    {
        if (shiftValueText == null)
            return;

        bool hasSelection = !string.IsNullOrEmpty(selectedShift);
        shiftValueText.text = hasSelection ? selectedShift : shiftPlaceholder;
        shiftValueText.color = hasSelection ? shiftSelectedColor : shiftPlaceholderColor;
    }

    private void KillShiftTweens()
    {
        KillPanelTween();

        if (shiftSelectionTween != null && shiftSelectionTween.IsActive())
            shiftSelectionTween.Kill();

        shiftSelectionTween = null;
        shiftFrameTween?.Kill();
        shiftFrameTween = null;
        if (shiftSelectorFrame != null) shiftSelectorFrame.color = shiftFrameColor;
    }

    private void KillPanelTween()
    {
        if (shiftPanelTween != null && shiftPanelTween.IsActive())
            shiftPanelTween.Kill();

        shiftPanelTween = null;
    }

    private void SetError(string message)
    {
        if (errorText != null && errorText.text != message)
            errorText.text = message;
    }

    private void ClearError()
    {
        if (errorText != null && !string.IsNullOrEmpty(errorText.text))
            errorText.text = string.Empty;
    }
}
