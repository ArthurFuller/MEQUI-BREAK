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

    [Tooltip("Campo obrigatório usado para registrar o turno do usuário.")]
    [SerializeField] private TMP_InputField shiftInput;

    [Header("Interface")]
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text errorText;

    private bool isSubmitting;

    private void Awake()
    {
        ConfigureInput(nameInput, PlayerManager.DisplayNameMaxLength, "Digite seu nome completo");
        ConfigureInput(storeInput, PlayerManager.StoreIdMaxLength, "Número ou nome da filial");
        ConfigureInput(shiftInput, PlayerManager.ShiftMaxLength, "Ex: Manhã, Tarde ou Noite");
    }

    private void OnEnable()
    {
        SetListeners(true);
        ClearError();
        UpdateSubmitState();
    }

    private void OnDisable()
    {
        SetListeners(false);
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
        string shift = shiftInput != null ? shiftInput.text : string.Empty;

        if (!player.TryCompleteRegistration(displayName, storeId, shift, out string error))
        {
            SetError(error);
            UpdateSubmitState();
            return;
        }

        isSubmitting = true;
        SetInputsInteractable(false);
        ClearError();
        AudioManager.Instance?.PlayConfirm();
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
        SetInputListeners(shiftInput, enabled);

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

        if (shiftInput != null)
        {
            if (enabled) shiftInput.onSubmit.AddListener(SubmitFromKeyboard);
            else shiftInput.onSubmit.RemoveListener(SubmitFromKeyboard);
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

    private void FocusShift(string _) => shiftInput?.ActivateInputField();

    private void SubmitFromKeyboard(string _)
    {
        if (submitButton == null || submitButton.interactable)
            Login();
    }

    private void UpdateSubmitState()
    {
        if (submitButton == null || isSubmitting)
            return;

        bool isValid = PlayerManager.TryValidateRegistration(
            nameInput != null ? nameInput.text : string.Empty,
            storeInput != null ? storeInput.text : string.Empty,
            shiftInput != null ? shiftInput.text : string.Empty,
            out _);

        if (submitButton.interactable != isValid)
            submitButton.interactable = isValid;
    }

    private void SetInputsInteractable(bool interactable)
    {
        if (nameInput != null) nameInput.interactable = interactable;
        if (storeInput != null) storeInput.interactable = interactable;
        if (shiftInput != null) shiftInput.interactable = interactable;
        if (submitButton != null) submitButton.interactable = interactable;
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
