using UnityEngine;

public sealed class PointsService : MonoBehaviour
{
    public static PointsService Instance { get; private set; }

    [SerializeField, Min(0)] private int participationPoints = 20;

    public int ParticipationPoints => participationPoints;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public int AwardParticipation()
    {
        return AwardParticipation(participationPoints);
    }

    /// <summary>Permite configurar o prêmio de uma atividade sem alterar as demais.</summary>
    public int AwardParticipation(int amount)
    {
        if (PlayerManager.Instance == null || amount <= 0)
            return 0;

        PlayerManager.Instance.AddBreakPoints(amount);
        PlayerManager.Instance.SaveProfile();
        return amount;
    }
}
