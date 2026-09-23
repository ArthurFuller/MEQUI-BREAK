using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrewMember : HierarchyDragHandle
{
    public enum Specialty
    {
        None,
        Preparation,
        Grill,
        Assembly,
        Delivery,
        Wash
    }

    [Header("Equipe — referências da cena")]
    [SerializeField] private RectTransform homeAnchor;
    [SerializeField] private KitchenStation startStation;
    [SerializeField] private GameObject absentOverlay;
    [SerializeField] private TMP_Text statusLabel;

    [Header("Equipe — balanceamento")]
    [SerializeField] private Specialty specialty;
    [SerializeField, Range(1f, 2f)] private float specialtyMultiplier = 1.25f;
    [SerializeField] private bool canBeAbsent = true;

    private ComboCrewController owner;
    private KitchenStation station;
    private KitchenStation stationBeforeAbsence;
    private int memberIndex;
    private bool absent;

    public KitchenStation Station => station;
    public KitchenStation StartStation => startStation;
    public bool IsAbsent => absent;
    public bool CanBeAbsent => canBeAbsent;
    public int MemberIndex => memberIndex;
    public bool Configured => DragConfigured && homeAnchor != null && absentOverlay != null && statusLabel != null;
    public string ConfigurationError => !DragConfigured ? DragConfigurationError
        : homeAnchor == null ? "homeAnchor"
        : absentOverlay == null ? "absentOverlay"
        : statusLabel == null ? "statusLabel"
        : null;

    protected override bool CanDrag => !absent && owner != null && owner.CanArrangeCrew(this);
    protected override Vector3 RestPosition => (station != null ? station.CrewAnchor : homeAnchor).position;

    public void Initialize(ComboCrewController controller, int index)
    {
        owner = controller;
        memberIndex = index;
        absent = false;
        station = null;
        stationBeforeAbsence = null;
        absentOverlay.SetActive(false);
        RefreshStatus();
        MoveHome(true);
    }

    public float WorkMultiplier(KitchenStation.StationType type)
    {
        if (absent) return 0f;
        return SpecialtyMatches(type) ? specialtyMultiplier : 1f;
    }

    public void SetStationFromController(KitchenStation value, bool instant = false)
    {
        station = value;
        RefreshStatus();
        MoveHome(instant);
    }

    public void SetAbsent(bool value)
    {
        if (!canBeAbsent || absent == value) return;

        absent = value;
        absentOverlay.SetActive(value);

        if (value)
        {
            stationBeforeAbsence = station;
            owner?.RemoveCrewForAbsence(this);
        }
        else
        {
            owner?.RestoreCrewAfterAbsence(this, stationBeforeAbsence);
            stationBeforeAbsence = null;
        }

        RefreshStatus();
        MoveHome(false);
    }

    protected override void HighlightTargets(bool visible)
    {
        owner?.HighlightStations(this, visible);
    }

    private bool SpecialtyMatches(KitchenStation.StationType type)
    {
        switch (specialty)
        {
            case Specialty.Preparation: return type == KitchenStation.StationType.Preparation;
            case Specialty.Grill: return type == KitchenStation.StationType.Grill;
            case Specialty.Assembly: return type == KitchenStation.StationType.Assembly;
            case Specialty.Delivery: return type == KitchenStation.StationType.Delivery;
            case Specialty.Wash: return type == KitchenStation.StationType.Wash;
            default: return false;
        }
    }

    private void RefreshStatus()
    {
        if (statusLabel == null) return;
        if (absent)
        {
            statusLabel.text = "AUSENTE";
            return;
        }

        statusLabel.text = station == null ? "DISPONÍVEL" : station.ShortName;
    }
}
