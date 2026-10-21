using UnityEngine;

/// <summary>Limites editáveis de um turno; a simulação usa apenas uma cópia sorteada destes valores.</summary>
[CreateAssetMenu(menuName = "Mequi Break/Combo Crew/Turn Profile")]
public sealed class ComboCrewTurnProfile : ScriptableObject
{
    public static ComboCrewTurnProfile CreateFallback(int difficulty)
    {
        ComboCrewTurnProfile profile = CreateInstance<ComboCrewTurnProfile>();
        profile.hideFlags = HideFlags.DontSave;
        if (difficulty == 0) return profile;
        if (difficulty == 1)
        {
            profile.title = "TURNO MOVIMENTADO";
            profile.availableWorkers = new Vector2Int(4, 5);
            profile.stationFailures = new Vector2Int(1, 2);
            profile.workerBreaks = new Vector2Int(1, 2);
            profile.stationCooldown = 14f;
            profile.workerCooldown = 16f;
            profile.repairSeconds = new Vector2(3.5f, 5f);
            profile.breakSeconds = new Vector2(13f, 18f);
            profile.orderSizeWeights = new Vector3(2f, 3f, 2f);
            return profile;
        }
        profile.title = "TURNO INTENSO";
        profile.availableWorkers = new Vector2Int(4, 5);
        profile.stationFailures = new Vector2Int(2, 4);
        profile.workerBreaks = new Vector2Int(2, 3);
        profile.maxConcurrentFailures = 2;
        profile.maxConcurrentBreaks = 2;
        profile.stationCooldown = 12f;
        profile.workerCooldown = 14f;
        profile.repairSeconds = new Vector2(4f, 6f);
        profile.breakSeconds = new Vector2(16f, 22f);
        profile.orderSizeWeights = new Vector3(1f, 2f, 3f);
        return profile;
    }

    public string title = "TURNO TRANQUILO";
    [Min(1)] public int selectionWeight = 1;
    [Tooltip("Total de personagens disponíveis, incluindo o jogador.")]
    public Vector2Int availableWorkers = new Vector2Int(4, 5);
    public Vector2Int stationFailures = new Vector2Int(0, 1);
    public Vector2Int workerBreaks = new Vector2Int(0, 1);
    [Min(1)] public int maxConcurrentFailures = 1;
    [Min(1)] public int maxConcurrentBreaks = 1;
    [Min(0f)] public float stationCooldown = 18f;
    [Min(0f)] public float workerCooldown = 20f;
    public Vector2 repairSeconds = new Vector2(3f, 4f);
    public Vector2 breakSeconds = new Vector2(10f, 15f);
    [Tooltip("Pesos relativos de pedidos com 1, 2 e 3 itens.")]
    public Vector3 orderSizeWeights = new Vector3(3f, 2f, 1f);
    [Tooltip("Multiplicadores do tempo de preparo para 1, 2 e 3 itens.")]
    public Vector3 orderTimeMultipliers = new Vector3(.85f, 1f, 1.2f);
}
