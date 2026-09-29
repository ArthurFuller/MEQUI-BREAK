using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;

public sealed class DashboardController : MonoBehaviour
{
    [Serializable] public sealed class TurnView
    {
        public Button button;
        public Text count;
        public RectTransform[] segments;
        public Text[] percentages;
    }

    [Serializable] public sealed class ActionSlot
    {
        public GameObject root;
        public Text subject;
        public Text next;
        public Text date;
        public Text reminder;
        public Button edit;
        public Button delete;
    }

    [Serializable] private sealed class ActionRecord
    {
        public string id;
        public string subject;
        public string next;
        public string date;
        public string reminder;
    }

    [Serializable] private sealed class ActionFile
    {
        public List<ActionRecord> items = new List<ActionRecord>();
    }

    private static readonly int[,,] Demo = {
        { {18,6,12,4}, {10,18,6,6}, {8,6,12,14} },
        { {12,6,10,8}, {8,14,4,10}, {6,6,12,8} },
        { {10,8,10,8}, {8,12,6,10}, {8,6,10,8} }
    };
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] CategoryNames = { "Hidratação", "Intervalo", "Alongamento", "Banheiro" };
    private static readonly string[] TurnNames = { "Todos os turnos", "Manhã", "Tarde", "Noite" };
    private static readonly string[] PeriodNames = { "Setembro 2026", "Agosto 2026" };
    private static readonly Color[] CategoryColors = {
        new Color32(0x86,0xc5,0xd4,0xff), new Color32(0xff,0xcf,0x32,0xff),
        new Color32(0xb2,0xcb,0x82,0xff), new Color32(0xd3,0x9d,0x85,0xff)
    };

    [Header("Estrutura existente na cena")]
    [SerializeField] private RectTransform safeAreaRect;
    [SerializeField] private RectTransform dashboardLayout;
    [SerializeField] private GameObject summaryPage;
    [SerializeField] private GameObject shiftsPage;
    [SerializeField] private GameObject actionsPage;
    [SerializeField] private GameObject actionList;
    [SerializeField] private GameObject actionForm;
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private Image[] tabBackgrounds;
    [SerializeField] private Text[] tabLabels;
    [SerializeField] private Button compareShiftsButton;
    [SerializeField] private Button followUpButton;
    [SerializeField] private Text greetingText;
    [SerializeField] private Text periodText;
    [SerializeField] private GameObject periodOptions;
    [SerializeField] private Button periodDropdownButton;
    [SerializeField] private Button septemberButton;
    [SerializeField] private Button augustButton;
    [SerializeField] private Text shiftText;
    [SerializeField] private GameObject shiftOptions;
    [SerializeField] private Button shiftDropdownButton;
    [SerializeField] private Button[] shiftButtons;
    [SerializeField] private Text summaryText;
    [SerializeField] private DashboardDonutGraphic donutGraphic;
    [SerializeField] private Text centerLarge;
    [SerializeField] private Text centerMiddle;
    [SerializeField] private Text centerSmall;
    [SerializeField] private Button[] categoryButtons;
    [SerializeField] private Image[] categoryHighlights;
    [SerializeField] private Text[] categoryPercents;
    [SerializeField] private TurnView[] turnViews;
    [SerializeField] private Button newActionButton;
    [SerializeField] private ActionSlot[] actionSlots;
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private Text actionPageText;
    [SerializeField] private Text actionCountText;
    [SerializeField] private GameObject emptyActionsText;
    [SerializeField] private Text actionFormTitle;
    [SerializeField] private InputField subjectInput;
    [SerializeField] private InputField nextInput;
    [SerializeField] private InputField dateInput;
    [SerializeField] private Text reminderText;
    [SerializeField] private GameObject reminderOptions;
    [SerializeField] private Button reminderDropdownButton;
    [SerializeField] private Button[] reminderButtons;
    [SerializeField] private Button saveActionButton;
    [SerializeField] private Button cancelActionButton;
    [SerializeField] private Text formErrorText;
    [SerializeField] private GameObject deleteConfirmPanel;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;

    private readonly List<ActionRecord> records = new List<ActionRecord>();
    private int selectedPage;
    private int selectedPeriod;
    private int selectedTurn;
    private int selectedCategory = -1;
    private int actionPage;
    private int editingIndex = -1;
    private int reminderChoice;
    private int pendingDeleteIndex = -1;
    private Vector2 lastSafeAreaSize;
    private string ActionPath => Path.Combine(Application.persistentDataPath, "dashboard_actions.json");
    private readonly Vector2[] pageBasePositions = new Vector2[3];
    private Sequence pageSequence;
    private bool pageInitialized;
    private const float PageEnterDuration = 0.24f;
    private Tween donutTween;
    private readonly float[] renderedShares = new float[4];
    private bool donutInitialized;
    private readonly Dictionary<GameObject, Sequence> dropdownTweens = new Dictionary<GameObject, Sequence>();
    private readonly Dictionary<GameObject, Vector2> dropdownPositions = new Dictionary<GameObject, Vector2>();
    private readonly Dictionary<GameObject, bool> dropdownOpen = new Dictionary<GameObject, bool>();

    private void Awake()
    {
        GameObject[] pages = { summaryPage, shiftsPage, actionsPage };
        for (int i = 0; i < pages.Length; i++)
            pageBasePositions[i] = ((RectTransform)pages[i].transform).anchoredPosition;
        for (int i = 0; i < tabButtons.Length; i++) {
            int page = i;
            tabButtons[i].onClick.AddListener(() => ShowPage(page));
        }
        compareShiftsButton.onClick.AddListener(() => ShowPage(1));
        followUpButton.onClick.AddListener(() => ShowPage(2));
        periodDropdownButton.onClick.AddListener(TogglePeriodOptions);
        shiftDropdownButton.onClick.AddListener(ToggleShiftOptions);
        reminderDropdownButton.onClick.AddListener(ToggleReminderOptions);
        septemberButton.onClick.AddListener(() => SelectPeriod(0));
        augustButton.onClick.AddListener(() => SelectPeriod(1));
        for (int i = 0; i < shiftButtons.Length; i++) {
            int turn = i;
            shiftButtons[i].onClick.AddListener(() => SelectTurn(turn));
        }
        for (int i = 0; i < categoryButtons.Length; i++) {
            int category = i;
            categoryButtons[i].onClick.AddListener(() => SelectCategory(category));
        }
        for (int i = 0; i < turnViews.Length; i++) {
            int turn = i + 1;
            turnViews[i].button.onClick.AddListener(() => { SelectTurn(turn); ShowPage(0); });
        }
        newActionButton.onClick.AddListener(() => OpenForm(-1));
        for (int i = 0; i < actionSlots.Length; i++) {
            int slot = i;
            actionSlots[i].edit.onClick.AddListener(() => OpenForm(actionPage * 2 + slot));
            actionSlots[i].delete.onClick.AddListener(() => DeleteAction(actionPage * 2 + slot));
        }
        previousPageButton.onClick.AddListener(() => { actionPage--; RefreshActions(); });
        nextPageButton.onClick.AddListener(() => { actionPage++; RefreshActions(); });
        saveActionButton.onClick.AddListener(SaveAction);
        cancelActionButton.onClick.AddListener(CloseForm);
        confirmDeleteButton.onClick.AddListener(ConfirmDelete);
        cancelDeleteButton.onClick.AddListener(() => deleteConfirmPanel.SetActive(false));
        for (int i = 0; i < reminderButtons.Length; i++) {
            int choice = i;
            reminderButtons[i].onClick.AddListener(() => { reminderChoice = choice; SetDropdown(reminderOptions, false); RefreshReminder(); });
        }
        LoadActions();
    }

    private void Update()
    {
        if (safeAreaRect != null && safeAreaRect.rect.size != lastSafeAreaSize) UpdateLayout();
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
            if (deleteConfirmPanel.activeSelf) deleteConfirmPanel.SetActive(false);
            else if (IsDropdownOpen(reminderOptions)) SetDropdown(reminderOptions, false);
            else if (IsDropdownOpen(shiftOptions)) SetDropdown(shiftOptions, false);
            else if (IsDropdownOpen(periodOptions)) SetDropdown(periodOptions, false);
            else if (actionForm.activeSelf) CloseForm();
            else FindFirstObjectByType<SceneLoader>()?.Load("HUB");
        }
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        UpdateLayout();
        InitializeDropdown(periodOptions);
        InitializeDropdown(shiftOptions);
        InitializeDropdown(reminderOptions);
        deleteConfirmPanel.SetActive(false);
        if (greetingText != null) greetingText.text = "Olá, Mariana";
        RefreshDashboard();
        ShowPage(0);
    }

    private void UpdateLayout()
    {
        if (safeAreaRect == null || dashboardLayout == null) return;
        lastSafeAreaSize = safeAreaRect.rect.size;
        float scale = Mathf.Min(lastSafeAreaSize.x / 390f, lastSafeAreaSize.y / 850f);
        dashboardLayout.localScale = Vector3.one * Mathf.Clamp(scale, 0.1f, 1f);
    }

    public void TogglePeriodOptions()
    {
        SetDropdown(shiftOptions, false);
        SetDropdown(periodOptions, !IsDropdownOpen(periodOptions));
    }

    public void ToggleShiftOptions()
    {
        SetDropdown(periodOptions, false);
        SetDropdown(shiftOptions, !IsDropdownOpen(shiftOptions));
    }

    public void ToggleReminderOptions()
    {
        SetDropdown(reminderOptions, !IsDropdownOpen(reminderOptions));
    }

    private bool IsDropdownOpen(GameObject menu)
    {
        return dropdownOpen.TryGetValue(menu, out bool open) && open;
    }

    private void InitializeDropdown(GameObject menu)
    {
        dropdownPositions[menu] = ((RectTransform)menu.transform).anchoredPosition;
        dropdownOpen[menu] = false;
        CanvasGroup group = menu.GetComponent<CanvasGroup>();
        if (group != null) {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        menu.SetActive(false);
    }

    private void SetDropdown(GameObject menu, bool open, bool instant = false)
    {
        if (menu == null) return;
        if (!open && !menu.activeSelf) instant = true;
        if (dropdownTweens.TryGetValue(menu, out Sequence current)) current?.Kill();
        dropdownOpen[menu] = open;
        RectTransform rect = (RectTransform)menu.transform;
        Vector2 rest = dropdownPositions[menu];
        CanvasGroup group = menu.GetComponent<CanvasGroup>();
        if (instant || group == null) {
            menu.SetActive(open);
            rect.anchoredPosition = rest;
            if (group != null) {
                group.alpha = open ? 1f : 0f;
                group.interactable = open;
                group.blocksRaycasts = open;
            }
            dropdownTweens.Remove(menu);
            return;
        }

        if (open && !menu.activeSelf) {
            rect.anchoredPosition = rest + Vector2.up * 18f;
            group.alpha = 0f;
        }
        if (open) menu.SetActive(true);
        group.interactable = false;
        group.blocksRaycasts = false;
        Sequence sequence = DOTween.Sequence().SetUpdate(UpdateType.Normal, UIMotionDefaults.UseUnscaledTime);
        dropdownTweens[menu] = sequence;
        if (open) {
            sequence.Join(rect.DOAnchorPos(rest, 0.22f).SetEase(Ease.OutCubic));
            sequence.Join(group.DOFade(1f, 0.18f).SetEase(Ease.OutQuad));
            sequence.OnComplete(() => {
                group.interactable = true;
                group.blocksRaycasts = true;
                dropdownTweens.Remove(menu);
            });
        } else {
            sequence.Join(rect.DOAnchorPos(rest + Vector2.up * 12f, 0.16f).SetEase(Ease.InOutCubic));
            sequence.Join(group.DOFade(0f, 0.14f).SetEase(Ease.InQuad));
            sequence.OnComplete(() => {
                menu.SetActive(false);
                rect.anchoredPosition = rest;
                dropdownTweens.Remove(menu);
            });
        }
    }

    private void SelectPeriod(int value)
    {
        selectedPeriod = Mathf.Clamp(value, 0, 1);
        selectedCategory = -1;
        SetDropdown(periodOptions, false);
        RefreshDashboard();
    }

    private void SelectTurn(int value)
    {
        selectedTurn = Mathf.Clamp(value, 0, 3);
        selectedCategory = -1;
        SetDropdown(shiftOptions, false);
        RefreshDashboard();
    }

    private void SelectCategory(int value)
    {
        selectedCategory = selectedCategory == value ? -1 : value;
        RefreshDashboard();
    }

    private void ShowPage(int page)
    {
        int nextPage = Mathf.Clamp(page, 0, 2);
        if (pageInitialized && nextPage == selectedPage) return;
        int previousPage = selectedPage;
        bool animate = pageInitialized;
        pageSequence?.Kill();
        pageSequence = null;
        if (actionForm.activeSelf) CloseForm();
        SetDropdown(periodOptions, false, true);
        SetDropdown(shiftOptions, false, true);
        selectedPage = nextPage;
        if (selectedPage == 2) RefreshActions();

        GameObject[] pages = { summaryPage, shiftsPage, actionsPage };
        for (int i = 0; i < pages.Length; i++) {
            RectTransform rect = (RectTransform)pages[i].transform;
            rect.anchoredPosition = pageBasePositions[i];
            CanvasGroup group = pages[i].GetComponent<CanvasGroup>();
            if (group != null) {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = i == nextPage && !animate;
            }
            pages[i].SetActive(i == nextPage);
        }

        CanvasGroup incoming = pages[nextPage].GetComponent<CanvasGroup>();
        if (animate) {
            RectTransform inRect = (RectTransform)pages[nextPage].transform;
            float direction = nextPage > previousPage ? 1f : -1f;
            inRect.anchoredPosition = pageBasePositions[nextPage] + Vector2.right * (32f * direction);
            pageSequence = DOTween.Sequence().SetUpdate(UpdateType.Normal, UIMotionDefaults.UseUnscaledTime);
            pageSequence.Append(inRect.DOAnchorPos(pageBasePositions[nextPage], PageEnterDuration).SetEase(Ease.OutCubic));
            pageSequence.OnComplete(() => {
                if (incoming != null) incoming.blocksRaycasts = true;
                pageSequence = null;
            });
        }
        for (int i = 0; i < tabButtons.Length; i++) {
            bool active = i == selectedPage;
            Color background = active ? new Color32(0xff,0xcf,0x32,0xff) : new Color32(0x2b,0x2b,0x2b,0xff);
            Color label = active ? new Color32(0x25,0x20,0x15,0xff) : new Color32(0xcc,0xcc,0xcc,0xff);
            tabBackgrounds[i].DOKill();
            tabLabels[i].DOKill();
            tabBackgrounds[i].color = background;
            tabLabels[i].color = label;
        }
        pageInitialized = true;
    }

    private void OnDisable()
    {
        pageSequence?.Kill();
        donutTween?.Kill();
        foreach (Sequence sequence in dropdownTweens.Values) sequence?.Kill();
        dropdownTweens.Clear();
        foreach (Image image in tabBackgrounds) if (image != null) image.DOKill();
        foreach (Text label in tabLabels) if (label != null) label.DOKill();
    }

    private void UpdateDonut(int[] values, int total)
    {
        if (donutGraphic == null) return;
        float[] target = new float[4];
        float change = 0f;
        for (int i = 0; i < 4; i++) {
            target[i] = values[i] / (float)Mathf.Max(1, total);
            change += Mathf.Abs(target[i] - renderedShares[i]);
        }
        if (donutInitialized && change < 0.0001f) return;
        donutTween?.Kill();
        if (!donutInitialized) {
            Array.Copy(target, renderedShares, 4);
            donutGraphic.SetShares(renderedShares);
            donutInitialized = true;
            return;
        }
        float[] from = (float[])renderedShares.Clone();
        donutTween = DOTween.To(() => 0f, t => {
            for (int i = 0; i < 4; i++) renderedShares[i] = Mathf.Lerp(from[i], target[i], t);
            donutGraphic.SetShares(renderedShares);
        }, 1f, 0.42f).SetEase(Ease.InOutCubic).SetUpdate(true)
        .OnComplete(() => {
            Array.Copy(target, renderedShares, 4);
            donutGraphic.SetShares(renderedShares);
            donutTween = null;
        });
    }

    private int[] Values(int period, int turn)
    {
        int[] result = new int[4];
        for (int t = 0; t < 3; t++) {
            if (turn != 0 && t != turn - 1) continue;
            for (int c = 0; c < 4; c++) result[c] += Demo[period, t, c];
        }
        return result;
    }

    private static int Total(int[] values) => values.Sum();

    private void RefreshDashboard()
    {
        periodText.text = PeriodNames[selectedPeriod];
        shiftText.text = TurnNames[selectedTurn];
        int[] current = Values(selectedPeriod, selectedTurn);
        int[] previous = Values(selectedPeriod + 1, selectedTurn);
        int total = Total(current);
        int storeTotal = Total(Values(selectedPeriod, 0));
        summaryText.text = $"<b>{storeTotal / 2}</b> sessões concluídas · <b>{storeTotal}</b> escolhas na loja";
        UpdateDonut(current, total);
        if (selectedCategory < 0) {
            centerLarge.text = total.ToString();
            centerMiddle.text = "escolhas";
            centerSmall.text = TurnNames[selectedTurn];
            centerLarge.color = Color.white;
            centerLarge.fontSize = 40;
            centerSmall.fontSize = 13;
            centerSmall.rectTransform.anchoredPosition = new Vector2(25,-169);
            centerSmall.rectTransform.sizeDelta = new Vector2(200,53);
            centerMiddle.rectTransform.anchoredPosition = new Vector2(25,-136);
            centerLarge.rectTransform.anchoredPosition = new Vector2(35,-85);
        } else {
            int c = selectedCategory;
            float now = current[c] * 100f / Mathf.Max(1, total);
            float before = previous[c] * 100f / Mathf.Max(1, Total(previous));
            float delta = Mathf.Round((now - before) * 10f) / 10f;
            centerLarge.text = (delta > 0 ? "+" : "") + delta.ToString("0.0", PtBr) + " p.p.";
            centerLarge.fontSize = 28;
            centerLarge.rectTransform.anchoredPosition = new Vector2(35,-100);
            centerMiddle.text = CategoryNames[c];
            centerMiddle.rectTransform.anchoredPosition = new Vector2(25,-65);
            centerSmall.text = (delta > 0 ? "Aumentou" : delta < 0 ? "Diminuiu" : "Sem alteração") + "\n" + before.ToString("0.0", PtBr) + "% → " + now.ToString("0.0", PtBr) + "%\nvs. " + (selectedPeriod == 0 ? "agosto" : "julho");
            centerSmall.fontSize = 12;
            centerSmall.rectTransform.anchoredPosition = new Vector2(50,-153);
            centerSmall.rectTransform.sizeDelta = new Vector2(150,58);
            centerLarge.color = CategoryColors[c];
        }
        for (int c = 0; c < 4; c++) {
            categoryPercents[c].text = (current[c] * 100f / Mathf.Max(1,total)).ToString("0.0", PtBr) + "%";
            categoryHighlights[c].enabled = c == selectedCategory;
        }
        for (int t = 0; t < 3; t++) {
            int[] values = Values(selectedPeriod, t + 1);
            int sum = Total(values);
            turnViews[t].count.text = sum + " escolhas";
            float width = 286f;
            float segmentX = 0f;
            for (int c = 0; c < 4; c++) {
                float percent = values[c] * 100f / Mathf.Max(1, sum);
                turnViews[t].segments[c].anchoredPosition = new Vector2(segmentX, 0f);
                float segmentWidth = width * percent / 100f;
                turnViews[t].segments[c].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, segmentWidth);
                segmentX += segmentWidth;
                turnViews[t].percentages[c].text = Mathf.RoundToInt(percent) + "%";
            }
        }
    }

    private void LoadActions()
    {
        try {
            if (!File.Exists(ActionPath)) return;
            var data = JsonUtility.FromJson<ActionFile>(File.ReadAllText(ActionPath));
            if (data?.items != null) records.AddRange(data.items);
        } catch (Exception e) { Debug.LogWarning("Dashboard: ações não carregadas: " + e.Message); }
    }

    private void PersistActions()
    {
        try { File.WriteAllText(ActionPath, JsonUtility.ToJson(new ActionFile { items = new List<ActionRecord>(records) })); }
        catch (Exception e) { Debug.LogError("Dashboard: ações não salvas: " + e.Message); }
    }

    private void RefreshActions()
    {
        int pages = Mathf.Max(1, Mathf.CeilToInt(records.Count / 2f));
        actionPage = Mathf.Clamp(actionPage, 0, pages - 1);
        actionCountText.text = records.Count.ToString();
        emptyActionsText.SetActive(records.Count == 0);
        actionPageText.text = $"{actionPage + 1} / {pages}";
        previousPageButton.interactable = actionPage > 0;
        nextPageButton.interactable = actionPage < pages - 1;
        for (int slot = 0; slot < actionSlots.Length; slot++) {
            int index = actionPage * 2 + slot;
            bool exists = index < records.Count;
            actionSlots[slot].root.SetActive(exists);
            if (!exists) continue;
            var item = records[index];
            actionSlots[slot].subject.text = item.subject;
            actionSlots[slot].next.text = item.next;
            actionSlots[slot].date.text = "Retorno: " + (string.IsNullOrWhiteSpace(item.date) ? "Sem data" : FormatDate(item.date));
            actionSlots[slot].reminder.text = item.reminder == "day" ? "Lembrete no dia" : item.reminder == "before" ? "Lembrete um dia antes" : "Sem lembrete";
        }
    }

    private static string FormatDate(string value)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
            return date.ToString("dd/MM/yyyy");
        return value;
    }

    private void OpenForm(int index)
    {
        editingIndex = index >= 0 && index < records.Count ? index : -1;
        var item = editingIndex < 0 ? null : records[editingIndex];
        subjectInput.text = item?.subject ?? "";
        nextInput.text = item?.next ?? "";
        dateInput.text = item?.date ?? "";
        reminderChoice = item?.reminder == "day" ? 1 : item?.reminder == "before" ? 2 : 0;
        actionFormTitle.text = editingIndex < 0 ? "Novo acompanhamento" : "Editar acompanhamento";
        formErrorText.text = "";
        RefreshReminder();
        actionList.SetActive(false);
        actionForm.SetActive(true);
    }

    private void CloseForm()
    {
        actionForm.SetActive(false);
        actionList.SetActive(true);
        SetDropdown(reminderOptions, false, true);
        RefreshActions();
    }

    private void RefreshReminder()
    {
        reminderText.text = reminderChoice == 1 ? "No dia" : reminderChoice == 2 ? "Um dia antes" : "Sem lembrete";
    }

    private void SaveAction()
    {
        string subject = subjectInput.text.Trim();
        string date = dateInput.text.Trim();
        if (subject.Length == 0) { formErrorText.text = "Informe o assunto."; return; }
        if (reminderChoice != 0 && date.Length == 0) { formErrorText.text = "Informe a data para o lembrete."; return; }
        if (date.Length > 0 && !DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _)) {
            formErrorText.text = "Use a data no formato AAAA-MM-DD."; return;
        }
        var item = editingIndex < 0 ? new ActionRecord { id = Guid.NewGuid().ToString("N") } : records[editingIndex];
        item.subject = subject.Substring(0, Mathf.Min(120, subject.Length));
        item.next = nextInput.text.Trim().Substring(0, Mathf.Min(600, nextInput.text.Trim().Length));
        item.date = date;
        item.reminder = reminderChoice == 1 ? "day" : reminderChoice == 2 ? "before" : "none";
        if (editingIndex < 0) { records.Insert(0, item); actionPage = 0; }
        PersistActions();
        CloseForm();
    }

    private void DeleteAction(int index)
    {
        if (index < 0 || index >= records.Count) return;
        pendingDeleteIndex = index;
        deleteConfirmPanel.SetActive(true);
    }

    private void ConfirmDelete()
    {
        if (pendingDeleteIndex >= 0 && pendingDeleteIndex < records.Count) {
            records.RemoveAt(pendingDeleteIndex);
            PersistActions();
            RefreshActions();
        }
        pendingDeleteIndex = -1;
        deleteConfirmPanel.SetActive(false);
    }
}



