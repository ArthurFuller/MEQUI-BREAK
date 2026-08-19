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
        if (PlayerManager.Instance == null)
            return 0;

        PlayerManager.Instance.AddBreakPoints(participationPoints);
        PlayerManager.Instance.SaveProfile();
        return participationPoints;
    }
}
