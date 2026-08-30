using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mantém a abertura visual dentro da cena de Login enquanto o Boot inicializa
/// os serviços globais. O botão de entrada oculta a abertura e libera o formulário.
/// </summary>
[DisallowMultipleComponent]
public sealed class SplashScreenController : MonoBehaviour
{
    [SerializeField] private GameObject splashRoot;
    [SerializeField] private Button enterButton;

    private void Awake()
    {
        if (splashRoot != null)
            splashRoot.SetActive(true);

    }

    private void OnEnable()
    {
        if (enterButton != null)
            enterButton.onClick.AddListener(Dismiss);
    }

    private void OnDisable()
    {
        if (enterButton != null)
            enterButton.onClick.RemoveListener(Dismiss);
    }

    public void Dismiss()
    {
        AudioManager.Instance?.PlayClick();

        if (splashRoot != null)
            splashRoot.SetActive(false);
    }
}
