using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta a tela de Configurações aos serviços persistentes e permite editar
/// Loja/Turno sem invalidar o primeiro login.
/// </summary>
public sealed class SettingsController : MonoBehaviour
{
    [Header("Preferências")]
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Toggle notificationsToggle;
    [SerializeField] private Toggle endOfShiftReminderToggle;

    [Header("Dados do funcionário")]
    [SerializeField] private Button workSettingsButton;
    [SerializeField] private GameObject workSettingsPanel;
    [SerializeField] private RectTransform workSettingsArrow;
    [SerializeField] private RectTransform settingsContent;
    [SerializeField] private TMP_InputField storeInput;
    [SerializeField] private TMP_Text shiftValueText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Animação da área de trabalho")]
    [SerializeField, Min(0.05f)] private float workPanelDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float arrowDuration = 0.2f;

    private RectTransform workPanelRect;
    private LayoutElement workPanelLayout;
    private float workPanelExpandedHeight;
    private Sequence workPanelSequence;
    private bool isWorkPanelOpen;

    private void Start()
    {
        SettingsManager settings = SettingsManager.Instance;
        if (settings != null)
        {
            musicToggle?.SetIsOnWithoutNotify(settings.MusicEnabled);
            sfxToggle?.SetIsOnWithoutNotify(settings.SFXEnabled);
            notificationsToggle?.SetIsOnWithoutNotify(settings.NotificationsEnabled);
            endOfShiftReminderToggle?.SetIsOnWithoutNotify(settings.EndOfShiftReminderEnabled);
        }

        PlayerManager player = PlayerManager.Instance;
        if (player != null)
        {
            storeInput?.SetTextWithoutNotify(player.StoreId);
            if (shiftValueText != null)
                shiftValueText.text = player.Shift;
        }

        CacheWorkPanel();
        SetWorkPanelOpen(false, true);
        BindListeners();
        RefreshReminderInteractable();
        SetFeedback(string.Empty);
    }

    private void BindListeners()
    {
        if (musicToggle != null) musicToggle.onValueChanged.AddListener(SetMusic);
        if (sfxToggle != null) sfxToggle.onValueChanged.AddListener(SetSFX);
        if (notificationsToggle != null) notificationsToggle.onValueChanged.AddListener(SetNotifications);
        if (endOfShiftReminderToggle != null) endOfShiftReminderToggle.onValueChanged.AddListener(SetEndOfShiftReminder);
        if (workSettingsButton != null) workSettingsButton.onClick.AddListener(ToggleWorkSettingsPanel);
        if (storeInput != null) storeInput.onEndEdit.AddListener(SetStore);
    }

    private void UnbindListeners()
    {
        if (musicToggle != null) musicToggle.onValueChanged.RemoveListener(SetMusic);
        if (sfxToggle != null) sfxToggle.onValueChanged.RemoveListener(SetSFX);
        if (notificationsToggle != null) notificationsToggle.onValueChanged.RemoveListener(SetNotifications);
        if (endOfShiftReminderToggle != null) endOfShiftReminderToggle.onValueChanged.RemoveListener(SetEndOfShiftReminder);
        if (workSettingsButton != null) workSettingsButton.onClick.RemoveListener(ToggleWorkSettingsPanel);
        if (storeInput != null) storeInput.onEndEdit.RemoveListener(SetStore);
    }

    public void ToggleWorkSettingsPanel()
    {
        SetWorkPanelOpen(!isWorkPanelOpen, false);
        AudioManager.Instance?.PlayClick();
    }

    private void CacheWorkPanel()
    {
        if (workSettingsPanel == null)
            return;

        workPanelRect = workSettingsPanel.transform as RectTransform;
        workPanelLayout = workSettingsPanel.GetComponent<LayoutElement>();
        if (workPanelLayout != null)
            workPanelExpandedHeight = Mathf.Max(1f, workPanelLayout.preferredHeight);

        if (workPanelRect != null)
            workPanelRect.pivot = new Vector2(workPanelRect.pivot.x, 1f);
    }

    private void SetWorkPanelOpen(bool isOpen, bool immediate)
    {
        isWorkPanelOpen = isOpen;
        workPanelSequence?.Kill();
        workPanelSequence = null;

        if (immediate)
        {
            ApplyWorkPanelState(isOpen);
            RebuildSettingsLayout();
            return;
        }

        if (workSettingsPanel == null || workPanelRect == null || workPanelLayout == null)
        {
            ApplyWorkPanelState(isOpen);
            RebuildSettingsLayout();
            return;
        }

        if (isOpen && !workSettingsPanel.activeSelf)
        {
            workPanelLayout.preferredHeight = 0f;
            workPanelRect.localScale = new Vector3(1f, 0f, 1f);
            workSettingsPanel.SetActive(true);
        }

        float targetHeight = isOpen ? workPanelExpandedHeight : 0f;
        float targetScale = isOpen ? 1f : 0f;
        float targetArrowAngle = isOpen ? -90f : 0f;

        workPanelSequence = DOTween.Sequence()
            .Join(DOTween.To(
                () => workPanelLayout.preferredHeight,
                value => workPanelLayout.preferredHeight = value,
                targetHeight,
                workPanelDuration))
            .Join(workPanelRect.DOScaleY(targetScale, workPanelDuration))
            .SetEase(isOpen ? Ease.OutCubic : Ease.InOutCubic)
            .OnUpdate(RebuildSettingsLayout)
            .OnComplete(() =>
            {
                if (!isOpen)
                    workSettingsPanel.SetActive(false);

                RebuildSettingsLayout();
                workPanelSequence = null;
            });

        if (workSettingsArrow != null)
        {
            workPanelSequence.Join(
                workSettingsArrow
                    .DOLocalRotate(new Vector3(0f, 0f, targetArrowAngle), arrowDuration)
                    .SetEase(Ease.OutCubic));
        }
    }

    private void ApplyWorkPanelState(bool isOpen)
    {
        if (workSettingsPanel != null)
            workSettingsPanel.SetActive(isOpen);

        if (workPanelLayout != null)
            workPanelLayout.preferredHeight = isOpen ? workPanelExpandedHeight : 0f;

        if (workPanelRect != null)
            workPanelRect.localScale = new Vector3(1f, isOpen ? 1f : 0f, 1f);

        if (workSettingsArrow != null)
            workSettingsArrow.localEulerAngles = new Vector3(0f, 0f, isOpen ? -90f : 0f);
    }

    private void RebuildSettingsLayout()
    {
        if (settingsContent == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(settingsContent);
    }

    public void SetMusic(bool enabled) => SettingsManager.Instance?.SetMusicEnabled(enabled);

    public void SetSFX(bool enabled)
    {
        SettingsManager.Instance?.SetSFXEnabled(enabled);

        // O mesmo toque que liga os efeitos pode chegar ao feedback visual
        // antes ou depois do callback do Toggle, dependendo da ordem dos
        // componentes. Disparar aqui garante confirmação audível já no
        // primeiro toque; o cooldown do AudioManager evita duplicação.
        if (enabled)
            AudioManager.Instance?.PlayClick();
    }

    public void SetNotifications(bool enabled)
    {
        SettingsManager.Instance?.SetNotificationsEnabled(enabled);
        if (!enabled)
            endOfShiftReminderToggle?.SetIsOnWithoutNotify(false);
        RefreshReminderInteractable();
    }

    public void SetEndOfShiftReminder(bool enabled) =>
        SettingsManager.Instance?.SetEndOfShiftReminderEnabled(enabled);

    public void SetStore(string value) => UpdateWorkData(value, PlayerManager.Instance?.Shift);

    public void SelectMorning() => SelectShift("Manhã");
    public void SelectAfternoon() => SelectShift("Tarde");
    public void SelectNight() => SelectShift("Noite");

    private void SelectShift(string shift) => UpdateWorkData(PlayerManager.Instance?.StoreId, shift);

    private void UpdateWorkData(string store, string shift)
    {
        PlayerManager player = PlayerManager.Instance;
        if (player == null)
            return;

        if (player.TryUpdateWorkData(store, shift, out string error))
        {
            storeInput?.SetTextWithoutNotify(player.StoreId);
            if (shiftValueText != null)
                shiftValueText.text = player.Shift;
            SetFeedback("Dados atualizados.");
            AudioManager.Instance?.PlayConfirm();
        }
        else
        {
            storeInput?.SetTextWithoutNotify(player.StoreId);
            SetFeedback(error);
        }
    }

    private void RefreshReminderInteractable()
    {
        if (endOfShiftReminderToggle != null)
            endOfShiftReminderToggle.interactable = notificationsToggle == null || notificationsToggle.isOn;
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    // Compatibilidade temporária até a cena antiga ser migrada no Editor.
    public void SetMusicVolume(float value) => SetMusic(value > 0.001f);
    public void SetSFXVolume(float value) => SetSFX(value > 0.001f);
    public void SetVibration(bool enabled) => SettingsManager.Instance?.SetVibration(enabled);

    private void OnDestroy()
    {
        UnbindListeners();
        workPanelSequence?.Kill();
    }
}
