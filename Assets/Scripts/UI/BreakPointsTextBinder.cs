using TMPro;
using UnityEngine;

/// <summary>Mantém o texto de PB sincronizado com o perfil do jogador.</summary>
[DisallowMultipleComponent]
public sealed class BreakPointsTextBinder : MonoBehaviour
{
    [SerializeField] private TMP_Text target;
    [SerializeField] private bool includeSuffix;

    private PlayerManager playerManager;

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        // Start garante a inscrição mesmo quando PlayerManager ainda não existia no OnEnable.
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        if (playerManager != null)
            playerManager.BreakPointsChanged -= SetValue;

        playerManager = null;
    }

    private void Subscribe()
    {
        PlayerManager current = PlayerManager.Instance;
        if (current == null || current == playerManager)
            return;

        if (playerManager != null)
            playerManager.BreakPointsChanged -= SetValue;

        playerManager = current;
        playerManager.BreakPointsChanged += SetValue;
    }

    private void Refresh()
    {
        int value = PlayerManager.Instance?.Profile?.BreakPoints ?? 0;
        SetValue(value);
    }

    private void SetValue(int value)
    {
        if (target != null)
        {
            if (includeSuffix)
                target.SetText("{0} PB", value);
            else
                target.SetText("{0}", value);
        }
    }
}
