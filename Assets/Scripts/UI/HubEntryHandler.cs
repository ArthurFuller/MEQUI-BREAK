using System.Collections;
using UnityEngine;

/// <summary>
/// Verifica Break Points pendentes ao carregar o HUB e inicia sua animação.
///
/// Em carregamentos aditivos, aguarda a transição terminar para manter a trajetória
/// das moedas ancorada ao layout final.
/// </summary>
public sealed class HubEntryHandler : MonoBehaviour
{
    [SerializeField] private PointAnimationManager pointAnimationManager;
    private PlayerManager player;
    private bool isSubscribed;

    private IEnumerator Start()
    {
        player = PlayerManager.Instance;
        if (player == null)
            yield break;

        int pending = player.PendingBreakPoints;

        if (pending <= 0)
            yield break;

        if (pointAnimationManager == null)
        {
            Debug.LogError(
                "[HubEntryHandler] PointAnimationManager não atribuído no Inspector.",
                this
            );

            yield break;
        }

        int finalValue = player.Profile?.BreakPoints ?? 0;
        int baseValue = finalValue - pending;

        // Exibe o saldo anterior durante a entrada do HUB.
        if (pointAnimationManager.PointsLabel != null)
            pointAnimationManager.PointsLabel.SetText("{0} PB", baseValue);

        // Separa a animação dos PB da transição aditiva da cena.
        while (SceneLoader.IsTransitionInProgress)
            yield return null;

        // Aguarda um frame para os RectTransforms assumirem suas posições finais.
        yield return null;
        Canvas.ForceUpdateCanvases();

        // Evita acessar referências destruídas durante a espera.
        if (this == null || pointAnimationManager == null)
            yield break;

        // Revalida os pontos caso outro sistema tenha alterado o estado durante a espera.
        pending = player.PendingBreakPoints;
        if (pending <= 0)
            yield break;

        finalValue = player.Profile?.BreakPoints ?? 0;
        baseValue = finalValue - pending;

        if (pointAnimationManager.PointsLabel != null)
            pointAnimationManager.PointsLabel.SetText("{0} PB", baseValue);

        // Limpa os pontos pendentes somente após o último pulso do contador.
        SubscribeToCompletion();
        pointAnimationManager.AnimatePoints(baseValue, pending);

        if (!pointAnimationManager.IsAnimating)
            UnsubscribeFromCompletion();
    }

    private void SubscribeToCompletion()
    {
        UnsubscribeFromCompletion();
        pointAnimationManager.OnAnimationComplete += HandleAnimationComplete;
        isSubscribed = true;
    }

    private void HandleAnimationComplete()
    {
        player?.ClearPendingPoints();
        UnsubscribeFromCompletion();
    }

    private void UnsubscribeFromCompletion()
    {
        if (!isSubscribed)
            return;

        if (pointAnimationManager != null)
            pointAnimationManager.OnAnimationComplete -= HandleAnimationComplete;

        isSubscribed = false;
    }

    private void OnDisable()
    {
        UnsubscribeFromCompletion();

        // Sair do HUB consome os pontos pendentes para impedir uma repetição ao retornar.
        PlayerManager currentPlayer = player != null ? player : PlayerManager.Instance;
        if (currentPlayer == null || currentPlayer.PendingBreakPoints <= 0)
            return;

        currentPlayer.ClearPendingPoints();

        // Se o objeto ainda estiver ativo durante a saída, deixa o saldo final visível.
        if (pointAnimationManager?.PointsLabel != null)
            pointAnimationManager.PointsLabel.SetText("{0} PB", currentPlayer.Profile?.BreakPoints ?? 0);
    }
}
