using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class KitchenStation : MonoBehaviour, IDropHandler
{
    public enum StationType
    {
        Preparation,
        Grill,
        Assembly,
        Delivery,
        Wash
    }

    [Header("Estação — referências da cena")]
    [SerializeField] private StationType stationType;
    [SerializeField] private RectTransform crewAnchor;
    [SerializeField] private RectTransform trayAnchor;
    [SerializeField] private Image progressFill;
    [SerializeField] private GameObject dropHighlight;
    [SerializeField] private GameObject blockedOverlay;

    [Header("Estação — balanceamento")]
    [SerializeField, Min(.1f)] private float workSeconds = 4f;

    private ComboCrewController owner;
    private CrewMember assignedCrew;
    private int orderId = -1;
    private int trayId = -1;
    private float progress;
    private bool broken;
    private float repairRemaining;
    private float repairDuration;
    private bool completionFeedbackPlayed;
    private Tween progressFeedback;
    private Tween stationFeedback;
    private Tween blockedFeedback;
    private Vector3 progressBaseScale;
    private Vector3 stationBaseScale;
    private Vector3 blockedBaseScale;

    public StationType Type => stationType;
    public RectTransform CrewAnchor => crewAnchor;
    public RectTransform TrayAnchor => trayAnchor;
    public CrewMember AssignedCrew => assignedCrew;
    public bool Busy => trayId >= 0;
    public bool IsBroken => broken;
    public int OrderId => orderId;
    public int TrayId => trayId;
    public float Progress => progress;
    public string ShortName => stationType == StationType.Preparation ? "PREPARO"
        : stationType == StationType.Grill ? "CHAPA"
        : stationType == StationType.Assembly ? "MONTAGEM"
        : stationType == StationType.Delivery ? "ENTREGA"
        : "LOUÇA";

    public bool Configured => crewAnchor != null && trayAnchor != null && progressFill != null
        && dropHighlight != null && blockedOverlay != null && workSeconds > 0f;
    public string ConfigurationError => crewAnchor == null ? "crewAnchor"
        : trayAnchor == null ? "trayAnchor"
        : progressFill == null ? "progressFill"
        : dropHighlight == null ? "dropHighlight"
        : blockedOverlay == null ? "blockedOverlay"
        : workSeconds <= 0f ? "workSeconds"
        : null;

    public void Initialize(ComboCrewController controller)
    {
        owner = controller;
        assignedCrew = null;
        broken = false;
        repairRemaining = 0f;
        repairDuration = 0f;
        completionFeedbackPlayed = false;
        ConfigureHorizontalBar(progressFill);
        progressBaseScale = progressFill.rectTransform.localScale;
        stationBaseScale = transform.localScale;
        blockedBaseScale = blockedOverlay.transform.localScale;
        blockedOverlay.SetActive(false);
        Highlight(false);
        ClearWork();
    }

    public void AssignCrew(CrewMember member)
    {
        assignedCrew = member;
        RefreshProgress();
    }

    public bool TryLoad(int newOrderId, int newTrayId)
    {
        if (Busy || newTrayId < 0) return false;
        orderId = newOrderId;
        trayId = newTrayId;
        progress = 0f;
        completionFeedbackPlayed = false;
        ResetProgressFeedback();
        RefreshProgress();
        return true;
    }

    public void ClearWork()
    {
        orderId = -1;
        trayId = -1;
        progress = 0f;
        completionFeedbackPlayed = false;
        ResetProgressFeedback();
        RefreshProgress();
    }

    public bool Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return false;

        if (broken)
        {
            if (assignedCrew != null && !assignedCrew.IsAbsent)
            {
                repairRemaining = Mathf.Max(0f, repairRemaining - deltaTime * assignedCrew.WorkMultiplier(stationType));
                if (repairDuration > 0f)
                    SetHorizontalProgress(progressFill, 1f - Mathf.Clamp01(repairRemaining / repairDuration));

                if (repairRemaining <= 0f)
                {
                    broken = false;
                    blockedOverlay.SetActive(false);
                    blockedFeedback?.Kill();
                    blockedOverlay.transform.localScale = blockedBaseScale;
                    RefreshProgress();
                    owner?.StationRepaired(this);
                }
            }
            return false;
        }

        if (!Busy || assignedCrew == null || assignedCrew.IsAbsent) return false;

        progress = Mathf.Clamp01(progress + deltaTime * assignedCrew.WorkMultiplier(stationType) / workSeconds);
        RefreshProgress();

        if (progress >= 1f && !completionFeedbackPlayed)
        {
            completionFeedbackPlayed = true;
            PlayCompletionFeedback();
        }

        return progress >= 1f;
    }

    public bool Break(float repairSeconds)
    {
        if (broken) return false;

        broken = true;
        repairDuration = Mathf.Max(.1f, repairSeconds);
        repairRemaining = repairDuration;
        blockedOverlay.SetActive(true);
        SetHorizontalProgress(progressFill, 0f);

        blockedFeedback?.Kill();
        blockedOverlay.transform.localScale = blockedBaseScale;
        blockedFeedback = blockedOverlay.transform
            .DOPunchScale(Vector3.one * .055f, .22f, 2, .35f)
            .OnComplete(() => blockedOverlay.transform.localScale = blockedBaseScale);
        return true;
    }

    public void ForceRepair()
    {
        broken = false;
        repairRemaining = 0f;
        repairDuration = 0f;
        blockedFeedback?.Kill();
        if (blockedOverlay != null)
        {
            blockedOverlay.transform.localScale = blockedBaseScale;
            blockedOverlay.SetActive(false);
        }
        RefreshProgress();
    }

    public void Highlight(bool value)
    {
        if (dropHighlight != null) dropHighlight.SetActive(value);
    }

    public void OnDrop(PointerEventData data)
    {
        if (owner == null || data.pointerDrag == null) return;
        CrewMember member = data.pointerDrag.GetComponent<CrewMember>();
        if (member != null && member.OwnsPointer(data))
            owner.AssignCrew(member, this);
    }

    private static void ConfigureHorizontalBar(Image image)
    {
        if (image == null) return;

        // As barras da cena usam uma imagem sólida sem sprite. Nesse caso, controlar
        // a largura do RectTransform é mais confiável do que depender do modo Filled da Image.
        image.type = Image.Type.Simple;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
        rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetHorizontalProgress(Image image, float value)
    {
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        Vector2 max = rect.anchorMax;
        max.x = Mathf.Clamp01(value);
        rect.anchorMax = max;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void PlayCompletionFeedback()
    {
        progressFeedback?.Kill();
        stationFeedback?.Kill();
        progressFill.rectTransform.localScale = progressBaseScale;
        transform.localScale = stationBaseScale;

        progressFeedback = progressFill.rectTransform
            .DOPunchScale(Vector3.one * .045f, .18f, 2, .25f)
            .OnComplete(() => progressFill.rectTransform.localScale = progressBaseScale);

        stationFeedback = transform
            .DOPunchScale(Vector3.one * .025f, .18f, 2, .2f)
            .OnComplete(() => transform.localScale = stationBaseScale);
    }

    private void ResetProgressFeedback()
    {
        progressFeedback?.Kill();
        if (progressFill != null) progressFill.rectTransform.localScale = progressBaseScale;
    }

    private void RefreshProgress()
    {
        if (progressFill != null && !broken)
            SetHorizontalProgress(progressFill, Busy ? progress : 0f);
    }

    private void OnDisable()
    {
        progressFeedback?.Kill();
        stationFeedback?.Kill();
        blockedFeedback?.Kill();
        if (progressFill != null) progressFill.rectTransform.localScale = progressBaseScale;
        transform.localScale = stationBaseScale;
        if (blockedOverlay != null) blockedOverlay.transform.localScale = blockedBaseScale;
    }
}
