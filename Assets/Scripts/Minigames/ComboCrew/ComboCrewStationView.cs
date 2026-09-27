using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Bancada fixa e alvo de funcionários. A barra segue o valor lógico recebido.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewStationView : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    public enum Alert { None, Warning, Broken, Repairing }
    [SerializeField] private RectTransform workAnchor;
    [SerializeField] private RectTransform handoffAnchor;
    [SerializeField] private Image progressFill;
    [SerializeField] private Image highlight;
    [SerializeField] private TMP_Text status;
    [SerializeField] private GameObject trayOnCounter;
    private ComboCrewController owner;
    private int index;
    private float shown;
    private RectTransform bar;
    private Color highlightColor;
    private Color progressColor;
    private bool completionPlayed;
    private bool dragHighlight;
    private Alert alert;
    public RectTransform WorkAnchor => workAnchor;
    public RectTransform HandoffAnchor => handoffAnchor;
    public bool Configured => workAnchor != null && progressFill != null && highlight != null
        && handoffAnchor != null && status != null && trayOnCounter != null;

    public void Bind(ComboCrewController controller, int stationIndex)
    {
        owner = controller;
        index = stationIndex;
        shown = 0f;
        bar = progressFill.rectTransform;
        highlightColor = highlight.color;
        progressColor = progressFill.color;
        completionPlayed = false;
        progressFill.type = Image.Type.Simple;
        SetBar(0f);
        highlight.gameObject.SetActive(false);
        trayOnCounter.SetActive(false);
    }

    public void Refresh(float progress, string message, bool hasTray)
    {
        float blend = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
        shown = Mathf.Lerp(shown, Mathf.Clamp01(progress), blend);
        SetBar(shown);
        if (progress < .9f) completionPlayed = false;
        if (progress >= .99f && shown >= .985f && !completionPlayed)
        {
            completionPlayed = true;
            progressFill.DOKill();
            progressFill.color = progressColor;
            progressFill.DOColor(new Color(1f, .87f, .43f), .16f)
                .SetLoops(2, LoopType.Yoyo).SetTarget(this);
        }
        status.text = message;
        trayOnCounter.SetActive(hasTray);
    }

    private void SetBar(float value)
    {
        Vector3 scale = bar.localScale;
        scale.x = Mathf.Clamp01(value);
        bar.localScale = scale;
    }

    public void ResetProgress()
    {
        progressFill.DOKill();
        progressFill.color = progressColor;
        completionPlayed = false;
        shown = 0f;
        SetBar(0f);
    }
    public void Highlight(bool value) { dragHighlight = value; RefreshHighlight(); }

    public void SetAlert(Alert level)
    {
        alert = level;
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        if (highlight == null) return;
        highlight.color = alert == Alert.Broken ? new Color(1f, .24f, .16f, .8f)
            : alert == Alert.Repairing ? new Color(.28f, .8f, 1f, .68f)
            : alert == Alert.Warning ? new Color(1f, .72f, .18f, .55f)
            : new Color(highlightColor.r, highlightColor.g, highlightColor.b, .5f);
        highlight.gameObject.SetActive(dragHighlight || alert != Alert.None);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            owner?.TryRepairStation(index);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var card = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<ComboCrewWorkerCard>() : null;
        if (owner == null) return;
        if (card != null && card.OwnsPointer(eventData))
        {
            if (owner.AssignWorker(card.WorkerIndex, index)) card.AcceptCurrentDrop();
            return;
        }
        var worker = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<ComboCrewWorkerStationDrag>() : null;
        if (worker != null && worker.OwnsPointer(eventData)
            && owner.AssignWorker(worker.WorkerIndex, index)) worker.AcceptCurrentDrop();
    }

    private void OnDisable() { if (progressFill != null) progressFill.DOKill(); }

}
