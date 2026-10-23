using UnityEngine;

/// <summary>Personagem adicional reutilizando ator e card existentes.</summary>
[CreateAssetMenu(menuName = "Mequi Break/Combo Crew/Worker Profile")]
public sealed class ComboCrewWorkerProfile : ScriptableObject
{
    public string displayName = "Equipe";
    public Sprite head;
    public Color bodyColor = Color.white;
    public Sprite face;
    public Sprite hat;
    [Tooltip("Ordem: Preparo, Chapa, Montagem, Entrega, Lava-louça.")]
    public float[] stationSpeed = { 1f, 1f, 1f, 1f, 1f };
    [Range(.7f, 1.3f)] public float repairTimeMultiplier = 1f;
    [Range(.7f, 1.3f)] public float pauseWeight = 1f;
    [Range(.7f, 1.3f)] public float transportSpeedMultiplier = 1f;
    [Range(.7f, 1.3f)] public float breakDurationMultiplier = 1f;
}
