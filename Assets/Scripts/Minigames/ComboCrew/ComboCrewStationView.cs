using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>Bancada fixa e alvo de funcionários. A barra segue o valor lógico recebido.</summary>
[DisallowMultipleComponent]
public sealed class ComboCrewStationView : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    public enum Alert { None, Warning, Broken, Repairing }
    private static readonly Color StartBarColor = new Color32(223, 77, 67, 255);
    private static readonly Color EndBarColor = new Color32(79, 187, 112, 255);
    [SerializeField, Tooltip("Mova PontoDeTrabalho na Hierarchy para ajustar onde o rosto do funcionário aparece nesta bancada.")]
    private RectTransform workAnchor;
    [SerializeField] private RectTransform handoffAnchor;
    [SerializeField] private Image progressFill;
    [SerializeField, Range(0f, 6f), Tooltip("Recuo do preenchimento sob o contorno preto da barra.")]
    private float progressBarInset = 1f;
    [SerializeField] private Image highlight;
    [SerializeField] private Image stationVisual;
    [SerializeField, Tooltip("Camada de trânsito dos personagens, acima das frentes e placas das bancadas.")]
    private Transform movementLayer;
    [SerializeField] private RectTransform[] pulseParts;
    [SerializeField] private Shader dropOutlineShader;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Image statusIcon;
    [SerializeField] private Sprite[] statusSprites;
    [SerializeField] private GameObject trayOnCounter;
    [Header("Balão fixo desta bancada") ]
    [SerializeField] private GameObject speechBubble;
    [SerializeField] private TMP_Text speechText;
    [SerializeField] private Vector3 speechOffset;
    private ComboCrewWorkerView speaker;
    public TMP_Text SpeechText => speechText;

    [Header("Animação e feedback da estação")]
    [SerializeField, Min(1f)] private float barSmoothing = 10f;
    [SerializeField, Min(.01f)] private float completionFlashSeconds = .16f;
    [SerializeField] private Color completionFlashColor = new Color(1f, .87f, .43f);
    [SerializeField] private Color warningStatusColor = new Color(1f, .78f, .25f);
    [SerializeField] private Color brokenStatusColor = new Color(1f, .36f, .24f);
    [SerializeField] private Color repairingStatusColor = new Color(.45f, .86f, 1f);
    [SerializeField] private Color repairingProgressColor = new Color(.35f, .82f, 1f);
    [SerializeField] private Color brokenHighlightColor = new Color(1f, .24f, .16f, .8f);
    [SerializeField] private Color repairingHighlightColor = new Color(.28f, .8f, 1f, .68f);
    [SerializeField] private Color warningHighlightColor = new Color(1f, .72f, .18f, .55f);
    [SerializeField, Range(1.01f, 1.04f)] private float dragPulseScale = 1.015f;
    [SerializeField, Min(.1f)] private float dragPulseSeconds = .48f;

    private ComboCrewController owner;
    private int index;
    private float shown;
    private RectTransform bar;
    private GameObject progressRoot;
    private Color highlightColor;
    private Vector3 stationVisualScale;
    private Vector2 stationVisualPosition;
    private Vector2[] pulsePartPositions;
    private Vector3[] pulsePartScales;
    private float currentPulseScale = 1f;
    private Color progressColor;
    private Color statusColor;
    private Vector3 statusScale;
    private bool completionPlayed;
    private bool dragHighlight;
    private bool dropHovered;
    private Image dropOutline;
    private Material dropOutlineMaterial;
    private Sprite outlinedSprite;
    private Vector2 outlinedSize;
    private UnityEngine.UI.Outline meshOutline;
    private readonly Vector3[] dragCorners = new Vector3[4];
    private Alert alert;
    private Tween statusPulse;
    private Tween dragPulse;
    private ComboCrewStationParticles particles;
    public RectTransform WorkAnchor => workAnchor;
    public RectTransform HandoffAnchor => handoffAnchor;
    public Transform ForegroundLayer => movementLayer;
    public bool Configured => workAnchor != null && progressFill != null && highlight != null
        && stationVisual != null
        && handoffAnchor != null && status != null && trayOnCounter != null
        && speechBubble != null && speechText != null;

    public void Bind(ComboCrewController controller, int stationIndex)
    {
        if (speechBubble != null) speechBubble.SetActive(false);
        speaker = null;
        owner = controller;
        index = stationIndex;
        shown = 0f;
        bar = progressFill.rectTransform;
        progressRoot = progressFill.transform.parent != null
            ? progressFill.transform.parent.gameObject : progressFill.gameObject;
        highlightColor = highlight.color;
        stationVisualScale = stationVisual.rectTransform.localScale;
        stationVisualPosition = stationVisual.rectTransform.anchoredPosition;
        int partCount = pulseParts != null ? pulseParts.Length : 0;
        pulsePartPositions = new Vector2[partCount];
        pulsePartScales = new Vector3[partCount];
        for (int i = 0; i < partCount; i++)
        {
            if (pulseParts[i] == null) continue;
            pulsePartPositions[i] = pulseParts[i].anchoredPosition;
            pulsePartScales[i] = pulseParts[i].localScale;
        }
        progressColor = StartBarColor;
        statusColor = status.color;
        statusScale = status.rectTransform.localScale;
        alert = Alert.None;
        particles = GetComponent<ComboCrewStationParticles>();
        progressFill.color = progressColor;
        status.color = statusColor;
        status.rectTransform.localScale = statusScale;
        completionPlayed = false;
        progressFill.type = Image.Type.Sliced;
        ComboCrewProgressBorder.Ensure(progressFill);
        SetBar(0f);
        SetProgressVisible(false);
        highlight.gameObject.SetActive(false);
        trayOnCounter.SetActive(false);
        CreateDropOutline();
    }

    private void CreateDropOutline()
    {
        if (dropOutline != null) return;
        meshOutline = stationVisual.GetComponent<UnityEngine.UI.Outline>();
        if (meshOutline != null) meshOutline.enabled = false;
        if (dropOutlineShader == null || !dropOutlineShader.isSupported)
        {
            if (meshOutline == null) meshOutline = stationVisual.gameObject.AddComponent<UnityEngine.UI.Outline>();
            meshOutline.effectColor = new Color(1f, .97f, .83f, .85f);
            meshOutline.effectDistance = new Vector2(5f, 5f);
            meshOutline.useGraphicAlpha = true;
            Debug.LogWarning("Combo Crew: shader do contorno indisponível; usando o contorno da UI.", this);
            return;
        }
        GameObject imageObject = new GameObject("ContornoDoDestino", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)imageObject.transform;
        RectTransform visualRect = stationVisual.rectTransform;
        rect.SetParent(visualRect, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = visualRect.pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        dropOutline = imageObject.GetComponent<Image>();
        dropOutlineMaterial = new Material(dropOutlineShader);
        dropOutline.material = dropOutlineMaterial;
        dropOutline.raycastTarget = false;
        dropOutline.color = new Color(1f, 1f, 1f, 0f);
        SyncOutlineSource();
        imageObject.SetActive(false);
    }

    private void SyncOutlineSource()
    {
        if (dropOutline == null) return;
        Vector2 size = stationVisual.rectTransform.rect.size;
        if (stationVisual.sprite == outlinedSprite && size == outlinedSize) return;
        outlinedSprite = stationVisual.sprite;
        outlinedSize = size;
        if (outlinedSprite == null)
        {
            dropOutline.sprite = null;
            return;
        }
        // A cópia usa o mesmo mesh e as mesmas UVs da Image visível.
        Vector4 uv = DataUtility.GetOuterUV(outlinedSprite);
        const float radius = 5f;
        float ux = (uv.z - uv.x) / Mathf.Max(1f, size.x);
        float uy = (uv.w - uv.y) / Mathf.Max(1f, size.y);
        dropOutline.sprite = outlinedSprite;
        dropOutline.type = stationVisual.type;
        dropOutline.preserveAspect = stationVisual.preserveAspect;
        dropOutline.useSpriteMesh = stationVisual.useSpriteMesh;
        dropOutlineMaterial.SetVector("_SourceUV", uv);
        dropOutlineMaterial.SetVector("_SampleStep", new Vector4(ux * radius, uy * radius));
    }

    public void ShowSpeech(ComboCrewWorkerView worker, string message)
    {
        if (speechBubble == null || speechText == null) return;
        speaker = worker;
        PositionSpeech();
        speechText.text = message;
        speechBubble.SetActive(true);
    }

    private void PositionSpeech()
    {
        if (speechBubble != null && workAnchor != null)
            speechBubble.transform.position = workAnchor.TransformPoint(speechOffset);
    }

    public void HideSpeech(ComboCrewWorkerView worker)
    {
        if (speaker != worker) return;
        speaker = null;
        if (speechBubble != null) speechBubble.SetActive(false);
    }

#if UNITY_EDITOR
    public void SetSpeechPreview(bool visible)
    {
        if (Application.isPlaying || speechBubble == null) return;
        if (UnityEditor.Selection.activeTransform == speechBubble.transform && workAnchor != null)
        {
            Vector3 editedOffset = workAnchor.InverseTransformPoint(speechBubble.transform.position);
            if ((editedOffset - speechOffset).sqrMagnitude > .001f)
            {
                UnityEditor.Undo.RecordObject(this, "Posicionar balão da bancada");
                speechOffset = editedOffset;
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
        PositionSpeech();
        speechBubble.SetActive(visible);
    }
#endif

    public void SetProgressVisible(bool visible)
    {
        if (progressRoot != null) progressRoot.SetActive(visible);
    }

    public void Refresh(float progress, string message, bool hasTray, bool washingAtSink = false)
    {
        if (speechBubble != null && speechBubble.activeSelf) PositionSpeech();
        SyncOutlineSource();
        float blend = 1f - Mathf.Exp(-barSmoothing * Time.unscaledDeltaTime);
        shown = progress >= 1f ? 1f : Mathf.Lerp(shown, Mathf.Clamp01(progress), blend);
        SetBar(shown);
        status.text = message;
        if (statusIcon != null && statusSprites != null && statusSprites.Length >= 8)
            statusIcon.sprite = statusSprites[StatusSpriteIndex(message)];
        particles?.SetWorking(alert == Alert.None &&
            (index == 4 ? washingAtSink : message == "Preparando"));
        if (alert == Alert.None)
        {
            status.color = message == "Preparando" ? new Color32(246, 193, 70, 255)
                : message == "Pronta" ? new Color32(102, 190, 119, 255)
                : message == "Lavando" ? repairingStatusColor : statusColor;
        }
        trayOnCounter.SetActive(hasTray);
    }

    private static int StatusSpriteIndex(string message) => message switch
    {
        "Sem funcionário" => 1,
        "Preparando" or "Lavando" => 2,
        "Transportando" or "Retornando" => 3,
        "Precisa de ajuste" or "Sem bandejas" or "Bandejas aguardando" or "Aguardando bancada" => 4,
        "Quebrada" => 5,
        "Em manutenção" => 6,
        "Ausente" or "Pausa em breve" => 7,
        _ => 0
    };

    private void SetBar(float value)
    {
        RectTransform track = (RectTransform)bar.parent;
        float inset = Mathf.Clamp(progressBarInset, 0f, track.rect.height * .5f);
        float availableWidth = Mathf.Max(0f, track.rect.width - inset * 2f);
        float width = availableWidth * Mathf.Clamp01(value);
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, .5f);
        bar.pivot = new Vector2(0f, .5f);
        bar.anchoredPosition = new Vector2(-track.rect.width * .5f + inset, 0f);
        bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(0f, track.rect.height - inset * 2f));
        bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        progressFill.enabled = width > .5f;
        progressFill.color = Color.Lerp(StartBarColor, EndBarColor, Mathf.Clamp01(value));
    }

    public void ResetProgress()
    {
        progressFill.DOKill();
        progressFill.color = StartBarColor;
        completionPlayed = false;
        shown = 0f;
        SetBar(0f);
    }
    public void Highlight(bool value)
    {
        dragHighlight = value;
        if (!value) SetDropHover(false);
        RefreshHighlight();
    }

    public bool ContainsDraggedVisual(RectTransform visual)
    {
        if (visual == null || transform is not RectTransform area) return false;
        visual.GetWorldCorners(dragCorners);
        Vector2 first = area.InverseTransformPoint(dragCorners[0]);
        float minX = first.x, maxX = first.x, minY = first.y, maxY = first.y;
        for (int i = 1; i < dragCorners.Length; i++)
        {
            Vector2 point = area.InverseTransformPoint(dragCorners[i]);
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }
        Rect target = area.rect;
        float width = Mathf.Max(0f, Mathf.Min(maxX, target.xMax) - Mathf.Max(minX, target.xMin));
        float height = Mathf.Max(0f, Mathf.Min(maxY, target.yMax) - Mathf.Max(minY, target.yMin));
        return width * height >= (maxX - minX) * (maxY - minY) * .22f;
    }

    public void SetDropHover(bool value)
    {
        if (dropHovered == value) return;
        dropHovered = value;
        if (meshOutline != null) meshOutline.enabled = value && dropOutline == null;
        if (dropOutline == null) return;
        if (value) SyncOutlineSource();
        dropOutline.DOKill();
        if (value)
        {
            dropOutline.gameObject.SetActive(true);
            dropOutline.DOFade(1f, .1f).SetUpdate(true);
        }
        else
        {
            dropOutline.DOFade(0f, .08f).SetUpdate(true)
                .OnComplete(() => { if (!dropHovered && dropOutline != null)
                    dropOutline.gameObject.SetActive(false); });
        }
    }

    public void SetAlert(Alert level)
    {
        statusPulse?.Kill();
        dragPulse?.Kill();
        statusPulse = null;
        dragPulse = null;
        alert = level;
        particles?.SetAlert(level == Alert.Broken ? 1 : level == Alert.Repairing ? 2 : 0);
        progressFill.color = Color.Lerp(StartBarColor, EndBarColor, shown);
        status.color = level switch
        {
            Alert.Warning => warningStatusColor,
            Alert.Broken => brokenStatusColor,
            Alert.Repairing => repairingStatusColor,
            _ => statusColor
        };
        status.rectTransform.localScale = statusScale;
        if (stationVisual != null)
            stationVisual.rectTransform.anchoredPosition = stationVisualPosition;
        RefreshHighlight();
        if (level == Alert.Warning || level == Alert.Broken)
        {
            statusPulse = status.rectTransform.DOScale(statusScale * 1.04f, .65f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(this);
        }
    }

    private void RefreshHighlight()
    {
        if (highlight == null) return;
        dragPulse?.Kill();
        dragPulse = null;
        ApplyDragPulse(1f);
        highlight.color = alert == Alert.Broken ? brokenHighlightColor
            : alert == Alert.Repairing ? repairingHighlightColor
            : alert == Alert.Warning ? warningHighlightColor
            : new Color(highlightColor.r, highlightColor.g, highlightColor.b, .5f);
        highlight.gameObject.SetActive(false);
        if (dragHighlight && alert == Alert.None && stationVisual != null)
        {
            dragPulse = DOTween.To(() => currentPulseScale, ApplyDragPulse,
                    dragPulseScale, dragPulseSeconds)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetTarget(this);
        }
    }

    private void ApplyDragPulse(float scale)
    {
        currentPulseScale = scale;
        if (stationVisual != null)
            stationVisual.rectTransform.localScale = stationVisualScale * scale;
        if (pulseParts == null || pulsePartPositions == null) return;
        for (int i = 0; i < pulseParts.Length; i++)
        {
            RectTransform part = pulseParts[i];
            if (part == null) continue;
            part.anchoredPosition = stationVisualPosition
                + (pulsePartPositions[i] - stationVisualPosition) * scale;
            part.localScale = pulsePartScales[i] * scale;
        }
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
            if (owner.AssignDraggedWorker(card.WorkerIndex, card.DragVisual)) card.AcceptCurrentDrop();
            return;
        }
        var worker = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<ComboCrewWorkerStationDrag>() : null;
        if (worker != null && worker.OwnsPointer(eventData)
            && owner.AssignDraggedWorker(worker.WorkerIndex, worker.Actor)) worker.AcceptCurrentDrop();
    }

    private void OnDisable()
    {
        speaker = null;
        if (speechBubble != null) speechBubble.SetActive(false);
        if (progressFill != null)
        {
            progressFill.DOKill();
            progressFill.color = progressColor;
        }
        statusPulse?.Kill();
        dragPulse?.Kill();
        if (dropOutline != null)
        {
            dropOutline.DOKill();
            dropOutline.gameObject.SetActive(false);
        }
        if (meshOutline != null) meshOutline.enabled = false;
        dropHovered = false;
        statusPulse = null;
        dragPulse = null;
        if (stationVisual != null)
        {
            ApplyDragPulse(1f);
            stationVisual.rectTransform.anchoredPosition = stationVisualPosition;
        }
        particles?.SetWorking(false);
        if (status != null)
        {
            status.color = statusColor;
            status.rectTransform.localScale = statusScale;
        }
    }

    private void OnDestroy()
    {
        if (dropOutlineMaterial != null) Destroy(dropOutlineMaterial);
    }

}
