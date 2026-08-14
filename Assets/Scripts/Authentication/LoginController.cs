using TMPro;
using UnityEngine;

/// <summary>
/// Demo login. Replace credential validation with an external service later.
/// </summary>
public sealed class LoginController : MonoBehaviour
{
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private TMP_InputField employeeIdInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text errorText;

    [Header("Demo Login")]
    [SerializeField] private string demoEmployeeId = "DEMO-1234";
    [SerializeField] private string demoPassword = "1234";
    [SerializeField] private string hubScene = "Hub";

    public void Login()
    {
        string employeeId = employeeIdInput != null
            ? employeeIdInput.text.Trim()
            : string.Empty;

        string password = passwordInput != null
            ? passwordInput.text
            : string.Empty;

        if (employeeId != demoEmployeeId || password != demoPassword)
        {
            SetError("Identificação ou senha inválida.");
            return;
        }

        if (!PlayerManager.Instance.Login(employeeId))
            return;

        PlayerManager.Instance.SaveProfile();

        AudioManager.Instance?.PlayConfirm();
        ClearError();

        sceneLoader.Load(hubScene);
    }

    private void SetError(string message)
    {
        if (errorText != null)
            errorText.text = message;
    }

    private void ClearError()
    {
        if (errorText != null)
            errorText.text = string.Empty;
    }
}