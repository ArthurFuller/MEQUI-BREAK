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

    [Header("Animação e feedback da estação")]
    [SerializeField, Min(1f)] private float barSmoothing = 10f;
    [SerializeField, Min(.01f)] private float completionFlashSeconds = .16f;
    [SerializeField] private Color completionFlashColor = new Color(1f, .87f, .43f);
    [SerializeField, Min(.01f)] private float brokenHighlightPulseSeconds = .32f;
    [SerializeField, Range(0f, 1f)] private float brokenHighlightMinAlpha = .34f;
    [SerializeField, Min(.01f)] private float brokenStatusPulseSeconds = .38f;
    [SerializeField, Range(1f, 1.2f)] private float brokenStatusScale = 1.08f;
    [SerializeField] private Ease brokenStatusPulseEase = Ease.InOutSine;
    [SerializeField] private Color warningStatusColor = new Color(1f, .78f, .25f);
    [SerializeField] private Color brokenStatusColor = new Color(1f, .36f, .24f);
    [SerializeField] private Color repairingStatusColor = new Color(.45f, .86f, 1f);
    [SerializeField] private Color repairingProgressColor = new Color(.35f, .82f, 1f);
    [SerializeField] private Color brokenHighlightColor = new Color(1f, .24f, .16f, .8f);
    [SerializeField] private Color repairingHighlightColor = new Color(.28f, .8f, 1f, .68f);
    [SerializeField] private Color warningHighlightColor = new Color(1f, .72f, .18f, .55f);

    private ComboCrewController owner;
    private int index;
    private float shown;
    private RectTransform bar;
    private Color highlightColor;
    private Color progressColor;
    private Color statusColor;
    private Vector3 statusScale;
    private bool completionPlayed;
    private bool dragHighlight;
    private Alert alert;
    private Tween brokenPulse;
    private Tween statusPulse;
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
        statusColor = status.color;
        statusScale = status.rectTransform.localScale;
        alert = Alert.None;
        progressFill.color = progressColor;
        status.color = statusColor;
        status.rectTransform.localScale = statusScale;
        completionPlayed = false;
        progressFill.type = Image.Type.Simple;
        SetBar(0f);
        highlight.gameObject.SetActive(false);
        trayOnCounter.SetActive(false);
    }

    public void Refresh(float progress, string message, bool hasTray)
    {
        float blend = 1f - Mathf.Exp(-barSmoothing * Time.unscaledDeltaTime);
        shown = Mathf.Lerp(shown, Mathf.Clamp01(progress), blend);
        SetBar(shown);
        if (progress < .9f) completionPlayed = false;
        if (progress >= .99f && shown >= .985f && !completionPlayed)
        {
            completionPlayed = true;
            progressFill.DOKill();
            progressFill.color = progressColor;
            progressFill.DOColor(completionFlashColor, completionFlashSeconds)
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
        progressFill.color = alert == Alert.Repairing ? repairingProgressColor : progressColor;
        completionPlayed = false;
        shown = 0f;
        SetBar(0f);
    }
    public void Highlight(bool value) { dragHighlight = value; RefreshHighlight(); }

    public void SetAlert(Alert level)
    {
        brokenPulse?.Kill();
        statusPulse?.Kill();
        brokenPulse = null;
        statusPulse = null;
        alert = level;
        progressFill.color = level == Alert.Repairing ? repairingProgressColor : progressColor;
        status.color = level switch
        {
            Alert.Warning => warningStatusColor,
            Alert.Broken => brokenStatusColor,
            Alert.Repairing => repairingStatusColor,
            _ => statusColor
        };
        status.rectTransform.localScale = statusScale;
        RefreshHighlight();
        if (level == Alert.Broken)
        {
            brokenPulse = highlight.DOFade(brokenHighlightMinAlpha, brokenHighlightPulseSeconds).SetLoops(-1, LoopType.Yoyo).SetTarget(this);
            statusPulse = status.rectTransform.DOScale(statusScale * brokenStatusScale, brokenStatusPulseSeconds)
                .SetEase(brokenStatusPulseEase).SetLoops(-1, LoopType.Yoyo).SetTarget(this);
        }
    }

    private void RefreshHighlight()
    {
        if (highlight == null) return;
        highlight.color = alert == Alert.Broken ? brokenHighlightColor
            : alert == Alert.Repairing ? repairingHighlightColor
            : alert == Alert.Warning ? warningHighlightColor
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

    private void OnDisable()
    {
        if (progressFill != null)
        {
            progressFill.DOKill();
            progressFill.color = progressColor;
        }
        brokenPulse?.Kill();
        statusPulse?.Kill();
        brokenPulse = null;
        statusPulse = null;
        if (status != null)
        {
            status.color = statusColor;
            status.rectTransform.localScale = statusScale;
        }
    }

}
