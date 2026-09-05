using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public sealed class SettingsToggleVisual : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private RectTransform handle;
    [SerializeField] private Image track;
    [SerializeField, Min(0f)] private float horizontalPadding = 5f;
    [SerializeField, Min(0.01f)] private float duration = 0.16f;
    [SerializeField] private Color onTrackColor = new Color(1f, 0.78f, 0f, 1f);
    [SerializeField] private Color offTrackColor = new Color(0.3f, 0.3f, 0.32f, 1f);
    [SerializeField] private Color onHandleColor = Color.white;
    [SerializeField] private Color offHandleColor = Color.white;

    private Tween positionTween;
    private Image handleImage;
    private bool lastKnownState;
    private bool stateInitialized;
    private float lastTrackWidth = -1f;
    private float lastHandleWidth = -1f;

    private void Reset()
    {
        toggle = GetComponent<Toggle>();
    }

    private void Awake()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (handle != null)
            handleImage = handle.GetComponent<Image>();
    }

    private void Update()
    {
        if (toggle == null)
            return;

        bool geometryChanged = handle != null
            && track != null
            && (!Mathf.Approximately(lastTrackWidth, track.rectTransform.rect.width)
                || !Mathf.Approximately(lastHandleWidth, handle.rect.width));

        // O layout ainda pode estar com largura zero no OnEnable; reaplica o estado no frame seguinte.
        if (geometryChanged)
            Refresh(toggle.isOn, true);
        else if (!stateInitialized || lastKnownState != toggle.isOn)
            Refresh(toggle.isOn, !stateInitialized);
    }

    private void OnEnable()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        if (toggle != null)
            toggle.onValueChanged.AddListener(Refresh);

        stateInitialized = false;
        Canvas.ForceUpdateCanvases();
        Refresh(toggle != null && toggle.isOn, true);
    }

    private void Start()
    {
        // Start pega a largura final calculada pelo LayoutGroup.
        Canvas.ForceUpdateCanvases();
        Refresh(toggle != null && toggle.isOn, true);
    }

    private void OnDisable()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(Refresh);

        KillTweens();
    }

    private void OnDestroy() => KillTweens();

    private void Refresh(bool isOn) => Refresh(isOn, false);

    private void Refresh(bool isOn, bool immediate)
    {
        if (handle == null || track == null)
            return;

        float offX = horizontalPadding;
        float trackWidth = track.rectTransform.rect.width;
        float handleWidth = handle.rect.width;
        float onX = Mathf.Max(offX, trackWidth - handleWidth - horizontalPadding);
        float targetX = isOn ? onX : offX;
        Color targetTrack = isOn ? onTrackColor : offTrackColor;
        Color targetHandle = isOn ? onHandleColor : offHandleColor;
        lastKnownState = isOn;
        stateInitialized = true;
        lastTrackWidth = trackWidth;
        lastHandleWidth = handleWidth;

        KillTweens();

        if (immediate || duration <= 0f)
        {
            Vector2 position = handle.anchoredPosition;
            position.x = targetX;
            handle.anchoredPosition = position;
            track.color = targetTrack;
            if (handleImage != null)
                handleImage.color = targetHandle;
            return;
        }

        track.color = targetTrack;
        if (handleImage != null)
            handleImage.color = targetHandle;

        positionTween = handle.DOAnchorPosX(targetX, duration).SetEase(Ease.OutQuad);
    }

    private void KillTweens()
    {
        positionTween?.Kill();
        positionTween = null;
    }
}
