using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

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
    [SerializeField, Min(0.05f)] private float shiftPanelDuration = 0.12f;
    [SerializeField] private Ease shiftPanelEase = Ease.OutCubic;
    [SerializeField, Min(0.03f)] private float shiftOptionDuration = 0.08f;
    [SerializeField, Range(0f, 0.15f)] private float shiftOptionStagger = 0.055f;
    [SerializeField, Range(0.9f, 1f)] private float shiftOptionStartScale = 0.96f;
    [SerializeField] private Ease shiftOptionEase = Ease.OutCubic;
    [SerializeField, Range(0f, 0.12f)] private float selectionPunchScale = 0.05f;

    [Header("Interface")]
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text errorText;

    private bool isSubmitting;
    private string selectedShift = string.Empty;
    private Tween shiftPanelTween;
    private Tween shiftSelectionTween;
    private float shiftPanelBackgroundAlpha = 1f;

    private void Awake()
    {
        ConfigureInput(nameInput, PlayerManager.DisplayNameMaxLength, "Digite seu nome completo");
        ConfigureInput(storeInput, PlayerManager.StoreIdMaxLength, "Número ou nome da filial");

        if (shiftPanelBackground != null)
            shiftPanelBackgroundAlpha = shiftPanelBackground.color.a;

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
            if (enabled) nameInput.onSubmit.AddListener(FocusStore);
            else nameInput.onSubmit.RemoveListener(FocusStore);
        }

        if (storeInput != null)
        {
            if (enabled) storeInput.onSubmit.AddListener(FocusShift);
            else storeInput.onSubmit.RemoveListener(FocusShift);
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

    private void FocusShift(string _)
    {
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

        if (shiftOptionsPanel.gameObject.activeSelf)
            CloseShiftOptions(immediate: false);
        else
            OpenShiftOptions();
    }

    private void OpenShiftOptions()
    {
        if (shiftOptionsPanel == null || shiftOptionsCanvasGroup == null || isSubmitting)
            return;

        KillPanelTween();
        shiftOptionsPanel.gameObject.SetActive(true);
        shiftOptionsPanel.localScale = new Vector3(1f, 0.995f, 1f);
        shiftOptionsCanvasGroup.alpha = 1f;
        shiftOptionsCanvasGroup.interactable = false;
        shiftOptionsCanvasGroup.blocksRaycasts = true;

        SetBackgroundAlpha(0f);
        PrepareOptionForReveal(morningOptionGroup);
        PrepareOptionForReveal(afternoonOptionGroup);
        PrepareOptionForReveal(nightOptionGroup);

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Join(shiftOptionsPanel.DOScale(Vector3.one, shiftPanelDuration).SetEase(shiftPanelEase));

        if (shiftPanelBackground != null)
            sequence.Join(shiftPanelBackground.DOFade(shiftPanelBackgroundAlpha, shiftPanelDuration)
                .SetEase(Ease.OutSine));

        float firstOptionTime = shiftPanelDuration;
        InsertOptionReveal(sequence, morningOptionGroup, firstOptionTime);
        InsertOptionReveal(sequence, afternoonOptionGroup, firstOptionTime + shiftOptionStagger);
        InsertOptionReveal(sequence, nightOptionGroup, firstOptionTime + shiftOptionStagger * 2f);

        sequence.OnComplete(() => shiftOptionsCanvasGroup.interactable = true);
        shiftPanelTween = sequence;
    }

    private void CloseShiftOptions(bool immediate)
    {
        if (shiftOptionsPanel == null || shiftOptionsCanvasGroup == null)
            return;

        KillPanelTween();
        shiftOptionsCanvasGroup.interactable = false;
        shiftOptionsCanvasGroup.blocksRaycasts = false;

        if (immediate || !shiftOptionsPanel.gameObject.activeSelf)
        {
            ResetClosedShiftPanel();
            return;
        }

        float closeDuration = shiftPanelDuration * 0.7f;
        shiftPanelTween = DOTween.Sequence()
            .Join(shiftOptionsCanvasGroup.DOFade(0f, closeDuration).SetEase(Ease.InSine))
            .Join(shiftOptionsPanel.DOScale(new Vector3(1f, 0.995f, 1f), closeDuration)
                .SetEase(Ease.InCubic))
            .OnComplete(ResetClosedShiftPanel)
            .SetUpdate(true);
    }

    private void PrepareOptionForReveal(CanvasGroup optionGroup)
    {
        if (optionGroup == null)
            return;

        optionGroup.alpha = 0f;
        optionGroup.transform.localScale = Vector3.one * shiftOptionStartScale;
    }

    private void InsertOptionReveal(Sequence sequence, CanvasGroup optionGroup, float atPosition)
    {
        if (optionGroup == null)
            return;

        sequence.InsertCallback(atPosition, () => optionGroup.alpha = 1f);
        sequence.Insert(atPosition,
            optionGroup.transform.DOScale(Vector3.one, shiftOptionDuration).SetEase(shiftOptionEase));
    }

    private void ResetClosedShiftPanel()
    {
        shiftOptionsCanvasGroup.alpha = 1f;
        shiftOptionsCanvasGroup.interactable = false;
        shiftOptionsCanvasGroup.blocksRaycasts = false;
        shiftOptionsPanel.localScale = Vector3.one;
        SetBackgroundAlpha(shiftPanelBackgroundAlpha);
        ResetOptionVisual(morningOptionGroup);
        ResetOptionVisual(afternoonOptionGroup);
        ResetOptionVisual(nightOptionGroup);
        shiftOptionsPanel.gameObject.SetActive(false);
    }

    private static void ResetOptionVisual(CanvasGroup optionGroup)
    {
        if (optionGroup == null)
            return;

        optionGroup.alpha = 1f;
        optionGroup.transform.localScale = Vector3.one;
    }

    private void SetBackgroundAlpha(float alpha)
    {
        if (shiftPanelBackground == null)
            return;

        Color color = shiftPanelBackground.color;
        color.a = alpha;
        shiftPanelBackground.color = color;
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
