using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FirstRunGuideSceneView : MonoBehaviour
{
    private const int EnergyStationStep = 2;

    [Header("Etapa desta cena")]
    [SerializeField, Range(0, 2)] private int stepIndex;
    [Tooltip("Permite que esta mesma interface manual exiba também a etapa seguinte na mesma cena.")]
    [SerializeField] private bool includesFollowingStep;

    [Header("Referências manuais")]
    [SerializeField] private Canvas guideCanvas;
    [SerializeField] private RectTransform root;
    [SerializeField] private Image highlightImage;
    [Tooltip("Destaque manual da etapa seguinte, quando as duas etapas acontecem nesta cena.")]
    [SerializeField] private Image followingStepHighlightImage;
    [Tooltip("Segundo destaque manual da Energy Station. Cada imagem mantém seu próprio RectTransform na Hierarchy.")]
    [SerializeField] private Image secondaryHighlightImage;
    [SerializeField] private RectTransform messagePanel;
    [SerializeField] private CanvasGroup messagePanelGroup;
    [SerializeField] private CanvasGroup skipButtonGroup;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button skipButton;

    [Header("Alvos da lógica")]
    [Tooltip("Botão da etapa, quando ele já existe na cena. A posição do destaque não depende desta referência.")]
    [SerializeField] private Button targetButton;
    [Tooltip("Botão da etapa seguinte, quando as duas etapas acontecem nesta cena.")]
    [SerializeField] private Button followingStepTargetButton;
    [SerializeField] private EnergyStationController energyStation;

    [Header("Pulso simples do destaque")]
    [SerializeField, Range(1f, 1.2f)] private float pulseScale = 1.04f;
    [SerializeField, Min(0.05f)] private float pulseDuration = 0.55f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;

    [Header("Testes")]
    [Tooltip("No Editor, exibe o tutorial mesmo depois de concluído. A build continua respeitando o primeiro acesso.")]
    [SerializeField] private bool executarSempreParaTeste;

    private Sequence pulseSequence;
    private Tween messageTween;
    private GraphicRaycaster guideRaycaster;
    private Vector3 originalHighlightScale = Vector3.one;
    private Vector3 originalFollowingHighlightScale = Vector3.one;
    private Vector3 originalSecondaryHighlightScale = Vector3.one;
    private Vector3 originalMessageScale = Vector3.one;
    private bool scaleCached;
    private int activeStep = -1;

    public int StepIndex => stepIndex;
    public Button SkipButton => skipButton;
    public EnergyStationController EnergyStation => energyStation;
    public bool ExecutarSempreParaTeste
    {
        get
        {
#if UNITY_EDITOR
            return executarSempreParaTeste;
#else
            return false;
#endif
        }
    }

    public bool IsReady => IsReadyForStep(stepIndex);

    public bool SupportsStep(int requestedStep)
    {
        return requestedStep == stepIndex
            || (includesFollowingStep && requestedStep == stepIndex + 1);
    }

    public bool IsReadyForStep(int requestedStep)
    {
        Image requestedHighlight = GetHighlight(requestedStep);
        Button requestedButton = GetTargetButton(requestedStep);

        return SupportsStep(requestedStep)
            && guideCanvas != null
            && root != null
            && requestedHighlight != null
            && messagePanel != null
            && messagePanelGroup != null
            && skipButtonGroup != null
            && progressText != null
            && messageText != null
            && skipButton != null
            && (requestedStep == EnergyStationStep
                ? secondaryHighlightImage != null
                : requestedButton != null);
    }

    public Button GetTargetButton(int requestedStep)
    {
        if (requestedStep == stepIndex)
            return targetButton;

        return includesFollowingStep && requestedStep == stepIndex + 1
            ? followingStepTargetButton
            : null;
    }

    private void Awake()
    {
        if (root == null)
            root = transform as RectTransform;

        if (guideCanvas == null)
            guideCanvas = GetComponent<Canvas>();

        guideRaycaster = GetComponent<GraphicRaycaster>();

        CacheOriginalScales();
        Hide();
    }

    private void OnDisable()
    {
        StopAnimations();
        RestoreVisualState();
    }

    public void Show(int requestedStep, int displayStep, int totalSteps, string message)
    {
        if (!IsReadyForStep(requestedStep))
            return;

        CacheOriginalScales();
        // Na primeira ativação, Awake chama Hide; por isso a etapa é aplicada depois.
        root.gameObject.SetActive(true);
        activeStep = requestedStep;
        SetIntroTargetActive(activeStep == stepIndex);
        progressText.SetText("GUIA {0} DE {1}", displayStep, totalSteps);
        messageText.text = message ?? string.Empty;

        Image selectedHighlight = GetHighlight(activeStep);
        highlightImage.gameObject.SetActive(highlightImage == selectedHighlight);
        if (followingStepHighlightImage != null
            && followingStepHighlightImage != highlightImage)
        {
            followingStepHighlightImage.gameObject.SetActive(
                followingStepHighlightImage == selectedHighlight);
        }
        if (secondaryHighlightImage != null)
            secondaryHighlightImage.gameObject.SetActive(false);
        guideCanvas.enabled = true;
        if (guideRaycaster != null)
            guideRaycaster.enabled = true;

        PlayEntrance();
        StartPulse(selectedHighlight);
    }

    public void SetMessage(string message)
    {
        if (messageText != null)
            messageText.text = message ?? string.Empty;
    }

    public void ShowEnergyHighlight(int completedInteractions)
    {
        if (activeStep != EnergyStationStep
            || highlightImage == null
            || secondaryHighlightImage == null)
            return;

        bool showSecond = completedInteractions > 0;
        highlightImage.gameObject.SetActive(!showSecond);
        secondaryHighlightImage.gameObject.SetActive(showSecond);
        StartPulse(showSecond ? secondaryHighlightImage : highlightImage);
    }

    public void HideEnergyHighlights()
    {
        if (activeStep != EnergyStationStep)
            return;

        StopPulse();

        if (highlightImage != null)
        {
            highlightImage.rectTransform.localScale = originalHighlightScale;
            highlightImage.gameObject.SetActive(false);
        }

        if (secondaryHighlightImage != null)
        {
            secondaryHighlightImage.rectTransform.localScale = originalSecondaryHighlightScale;
            secondaryHighlightImage.gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        StopAnimations();
        RestoreVisualState();
        SetIntroTargetActive(false);

        if (highlightImage != null)
            highlightImage.gameObject.SetActive(false);
        if (followingStepHighlightImage != null)
            followingStepHighlightImage.gameObject.SetActive(false);
        if (secondaryHighlightImage != null)
            secondaryHighlightImage.gameObject.SetActive(false);
        if (guideCanvas != null)
            guideCanvas.enabled = false;
        if (guideRaycaster != null)
            guideRaycaster.enabled = false;

        activeStep = -1;
    }

    private void SetIntroTargetActive(bool active)
    {
        if (includesFollowingStep
            && targetButton != null
            && targetButton != followingStepTargetButton)
        {
            targetButton.gameObject.SetActive(active);
        }
    }

    private void StartPulse(Image activeHighlight)
    {
        if (activeHighlight == null)
            return;

        StopPulse();

        Vector3 originalScale = GetOriginalScale(activeHighlight);
        RectTransform highlight = activeHighlight.rectTransform;
        highlight.localScale = originalScale;
        pulseSequence = DOTween.Sequence()
            .Append(highlight.DOScale(originalScale * pulseScale, pulseDuration).SetEase(pulseEase))
            .Append(highlight.DOScale(originalScale, pulseDuration).SetEase(pulseEase))
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
    }

    private void PlayEntrance()
    {
        if (messagePanelGroup == null || messagePanel == null || skipButtonGroup == null)
            return;

        if (messageTween != null && messageTween.IsActive())
            messageTween.Kill();

        messagePanelGroup.alpha = 0f;
        skipButtonGroup.alpha = 0f;
        messagePanel.localScale = originalMessageScale * 0.96f;

        messageTween = DOTween.Sequence()
            .Join(messagePanelGroup.DOFade(1f, 0.2f))
            .Join(messagePanel.DOScale(originalMessageScale, 0.24f).SetEase(Ease.OutCubic))
            .Append(skipButtonGroup.DOFade(1f, 0.12f))
            .SetUpdate(true);
    }

    private void StopAnimations()
    {
        StopPulse();

        if (messageTween != null && messageTween.IsActive())
            messageTween.Kill();

        messageTween = null;
    }

    private void StopPulse()
    {
        if (pulseSequence != null && pulseSequence.IsActive())
            pulseSequence.Kill();

        pulseSequence = null;
    }

    private void RestoreVisualState()
    {
        if (highlightImage != null)
            highlightImage.rectTransform.localScale = originalHighlightScale;
        if (followingStepHighlightImage != null)
            followingStepHighlightImage.rectTransform.localScale = originalFollowingHighlightScale;
        if (secondaryHighlightImage != null)
            secondaryHighlightImage.rectTransform.localScale = originalSecondaryHighlightScale;
        if (messagePanel != null)
            messagePanel.localScale = originalMessageScale;
        if (messagePanelGroup != null)
            messagePanelGroup.alpha = 1f;
        if (skipButtonGroup != null)
            skipButtonGroup.alpha = 1f;
    }

    private void CacheOriginalScales()
    {
        if (scaleCached)
            return;

        if (highlightImage != null)
            originalHighlightScale = highlightImage.rectTransform.localScale;
        if (followingStepHighlightImage != null)
            originalFollowingHighlightScale = followingStepHighlightImage.rectTransform.localScale;
        if (secondaryHighlightImage != null)
            originalSecondaryHighlightScale = secondaryHighlightImage.rectTransform.localScale;
        if (messagePanel != null)
            originalMessageScale = messagePanel.localScale;

        scaleCached = true;
    }

    private Image GetHighlight(int requestedStep)
    {
        if (requestedStep == stepIndex)
            return highlightImage;

        return includesFollowingStep && requestedStep == stepIndex + 1
            ? followingStepHighlightImage
            : null;
    }

    private Vector3 GetOriginalScale(Image image)
    {
        if (image == secondaryHighlightImage)
            return originalSecondaryHighlightScale;
        if (image == followingStepHighlightImage)
            return originalFollowingHighlightScale;
        return originalHighlightScale;
    }

#if UNITY_EDITOR
    public void ConfigureReferences(
        int sceneStep,
        Canvas canvas,
        RectTransform rootRect,
        Image highlight,
        Image secondaryHighlight,
        RectTransform panel,
        CanvasGroup panelGroup,
        CanvasGroup skipGroup,
        TMP_Text progress,
        TMP_Text message,
        Button skip,
        Button sceneTargetButton,
        EnergyStationController station,
        bool alsoIncludesFollowingStep = false,
        Image followingHighlight = null,
        Button followingTargetButton = null)
    {
        stepIndex = sceneStep;
        guideCanvas = canvas;
        root = rootRect;
        highlightImage = highlight;
        secondaryHighlightImage = secondaryHighlight;
        messagePanel = panel;
        messagePanelGroup = panelGroup;
        skipButtonGroup = skipGroup;
        progressText = progress;
        messageText = message;
        skipButton = skip;
        targetButton = sceneTargetButton;
        energyStation = station;
        includesFollowingStep = alsoIncludesFollowingStep;
        followingStepHighlightImage = followingHighlight;
        followingStepTargetButton = followingTargetButton;
        executarSempreParaTeste = false;
    }
#endif
}
