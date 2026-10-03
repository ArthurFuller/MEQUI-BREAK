using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class DashboardExperimentController : MonoBehaviour
{
    private sealed class View
    {
        public GameObject Root;
        public GameObject Card;
        public GameObject Comparison;
        public RectTransform Content;
        public ScrollRect Scroll;
        public string Activity;
        public Tween DonutTween;
        public readonly float[] RenderedShares = new float[4];
        public bool DonutInitialized;
    }

    private static readonly string[] TurnNames = { "Todos os turnos", "Manhã", "Tarde", "Noite" };
    private static readonly string[] EnergyNames = { "Hidratação", "Intervalo", "Alongamento", "Banheiro" };
    private static readonly Color Gold = new Color32(0xff, 0xcf, 0x32, 0xff);
    private static readonly Color Inactive = new Color32(0x2b, 0x2b, 0x2b, 0xff);
    private static readonly Color[] EnergyPalette = {
        new Color32(0x86,0xc5,0xd4,0xff), Gold,
        new Color32(0xb2,0xcb,0x82,0xff), new Color32(0xd3,0x9d,0x85,0xff)
    };
    private static readonly Color[] OutcomePalette = { Gold, new Color32(0x79,0x79,0x79,0xff), Color.clear, Color.clear };
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private DashboardController legacy;
    private Transform layout, summaryPage, tabs, periodOptions, shiftOptions;
    private Text periodText, shiftText, storeText, summaryCounts, conversationText;
    private Button periodButton, shiftButton;
    private readonly Button[] nav = new Button[5];
    private readonly View[] views = new View[3];
    private readonly GameObject[] tabSections = new GameObject[4];
    private bool tabsInitialized;
    private RectTransform pagesContainer;
    private CanvasGroup pagesGroup;
    private Vector2 pagesBasePosition;
    private Sequence tabTransition;
    private const float TabTransitionDuration = 0.34f;
    private const float TabTransitionDistance = 64f;
    private GameObject overview, conversationPanel;
    private Text[] overviewLines;
    private int selectedTab, selectedShift, monthOffset, selectedCategory = -1;
    private bool demonstration;
    private DashboardCloudData cloud;

    private void Start()
    {
        legacy = GetComponent<DashboardController>();
        GameObject layoutObject = GameObject.Find("DashboardLayout");
        if (legacy == null || layoutObject == null)
        {
            enabled = false;
            return;
        }
        legacy.EnableExternalDataMode();
        layout = layoutObject.transform;
        pagesContainer = (RectTransform)Find(layout, "Pages");
        if (pagesContainer == null) { enabled = false; return; }
        pagesGroup = pagesContainer.GetComponent<CanvasGroup>();
        if (pagesGroup == null) pagesGroup = pagesContainer.gameObject.AddComponent<CanvasGroup>();
        pagesBasePosition = pagesContainer.anchoredPosition;
        summaryPage = Find(layout, "Pages/SummaryPage");
        tabs = Find(layout, "Tabs");
        periodOptions = Find(layout, "PeriodOptions");
        if (summaryPage == null || tabs == null || periodOptions == null)
        {
            enabled = false;
            return;
        }
        periodButton = Find(layout, "PeriodDropdownButton").GetComponent<Button>();
        periodText = Find(layout, "PeriodDropdownButton/Label").GetComponent<Text>();
        storeText = Find(layout, "StoreLabel").GetComponent<Text>();
        summaryCounts = Find(summaryPage, "SummaryCounts").GetComponent<Text>();
        BuildNavigation();
        BuildPeriodMenu();
        BuildPages();
        BuildGlobalFilters();
        WireEnergyFilters();
        ShowTab(0);
        StartCloudData();
    }

    private void OnEnable()
    {
        FirebaseSessionSync.ReadyChanged += StartCloudData;
        StartCloudData();
    }

    private void StartCloudData()
    {
        if (!isActiveAndEnabled || layout == null || cloud != null || FirebaseSessionSync.Database == null)
            return;
        cloud = new DashboardCloudData(PlayerManager.Instance?.StoreId, DateTime.Now, Refresh);
        Refresh();
    }

    private void OnDisable()
    {
        FirebaseSessionSync.ReadyChanged -= StartCloudData;
        cloud?.Dispose();
        cloud = null;
        tabTransition?.Kill();
        if (pagesContainer != null) pagesContainer.anchoredPosition = pagesBasePosition;
        if (pagesGroup != null) { pagesGroup.alpha = 1f; pagesGroup.blocksRaycasts = true; }
        foreach (View view in views) view?.DonutTween?.Kill();
    }

    private static Transform Find(Transform root, string path)
    {
        return root != null ? root.Find(path) : null;
    }

    private void BuildNavigation()
    {
        nav[0] = Find(tabs, "ResumoTabButton").GetComponent<Button>();
        nav[1] = Find(tabs, "TurnosTabButton").GetComponent<Button>();
        nav[4] = Find(tabs, "AçõesTabButton").GetComponent<Button>();
        nav[2] = Instantiate(nav[1], tabs);
        nav[2].name = "RushTabButton";
        nav[3] = Instantiate(nav[1], tabs);
        nav[3].name = "ComboTabButton";
        RectTransform tabsRect = (RectTransform)tabs;
        tabsRect.sizeDelta = new Vector2(350, 48);
        SetSliced(tabs.GetComponent<Image>(), 2);
        string[] names = { "Resumo", "Energy", "Rush", "Combo", "Ações" };
        for (int i = 0; i < nav.Length; i++)
        {
            int tab = i;
            nav[i].onClick.RemoveAllListeners();
            nav[i].onClick.AddListener(() => ShowTab(tab));
            nav[i].transition = Selectable.Transition.None;
            RectTransform rect = (RectTransform)nav[i].transform;
            rect.sizeDelta = new Vector2(66, 34);
            rect.anchoredPosition = new Vector2(37 + i * 69, -24);
            SetSliced(nav[i].GetComponent<Image>(), 3);
            Text label = nav[i].GetComponentInChildren<Text>(true);
            label.text = names[i];
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
        }
    }

    private void BuildPeriodMenu()
    {
        Button current = Find(periodOptions, "SeptemberButton").GetComponent<Button>();
        Button previous = Find(periodOptions, "AugustButton").GetComponent<Button>();
        Button demo = Instantiate(previous, periodOptions);
        demo.name = "DemonstrationButton";
        ((RectTransform)periodOptions).sizeDelta = new Vector2(193, 136);
        ((RectTransform)demo.transform).anchoredPosition = new Vector2(96.5f, -110);
        current.GetComponentInChildren<Text>(true).text = MonthName(DateTime.Now);
        previous.GetComponentInChildren<Text>(true).text = MonthName(DateTime.Now.AddMonths(-1));
        demo.GetComponentInChildren<Text>(true).text = "Demonstração";
        current.onClick.AddListener(() => { demonstration = false; monthOffset = 0; selectedCategory = -1; Refresh(); });
        previous.onClick.AddListener(() => { demonstration = false; monthOffset = 1; selectedCategory = -1; Refresh(); });
        demo.onClick.RemoveAllListeners();
        demo.onClick.AddListener(() =>
        {
            legacy.SelectPeriod(0);
            demonstration = true;
            monthOffset = 0;
            selectedCategory = -1;
            Refresh();
        });
    }

    private void BuildPages()
    {
        GameObject energyCard = Find(summaryPage, "EnergyCard").gameObject;
        GameObject shiftsCard = Find(layout, "Pages/ShiftsPage/ShiftsCard").gameObject;
        Find(summaryPage, "CompareShiftsButton").gameObject.SetActive(false);
        overview = CreateOverview(energyCard);
        views[0] = CreateView("EnergyView", energyCard, shiftsCard, "energy_station", true);
        views[1] = CreateView("RushView", Instantiate(energyCard), Instantiate(shiftsCard), "rush_balance", false);
        views[2] = CreateView("ComboView", Instantiate(energyCard), Instantiate(shiftsCard), "combo_crew", false);
        ConfigureGameView(views[1], "Rush Balance", "Atendidos", "Perdidos");
        ConfigureGameView(views[2], "Combo Crew", "Atendidos", "Perdidos");
        tabSections[0] = CreateTabSection("OverviewSection", overview, conversationPanel);
        tabSections[1] = CreateTabSection("EnergySection", summaryCounts.gameObject, views[0].Root);
        tabSections[2] = CreateTabSection("RushSection", views[1].Root);
        tabSections[3] = CreateTabSection("ComboSection", views[2].Root);
    }

    private GameObject CreateTabSection(string name, params GameObject[] children)
    {
        GameObject section = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        section.transform.SetParent(summaryPage, false);
        RectTransform rect = (RectTransform)section.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        foreach (GameObject child in children) child.transform.SetParent(section.transform, false);
        return section;
    }

    private GameObject CreateOverview(GameObject template)
    {
        GameObject result = new GameObject("Overview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        result.transform.SetParent(summaryPage, false);
        RectTransform rect = (RectTransform)result.transform;
        Place(rect, 20, -310, 350, 310);
        Image source = template.GetComponent<Image>();
        Image image = result.GetComponent<Image>();
        image.sprite = source.sprite;
        image.color = source.color;
        SetSliced(image, 2);
        image.raycastTarget = false;
        Text titleTemplate = Find(template.transform, "EnergyTitle").GetComponent<Text>();
        overviewLines = new Text[7];
        string[] initial = { "O QUE OS JOGOS MOSTRARAM", "ENERGY STATION", "", "RUSH BALANCE", "", "COMBO CREW", "" };
        int[] y = { -19, -61, -86, -140, -165, -219, -244 };
        for (int i = 0; i < initial.Length; i++)
        {
            Text line = Instantiate(titleTemplate, result.transform);
            line.name = "OverviewLine" + i;
            line.text = initial[i];
            line.fontSize = i == 0 ? 17 : (i % 2 == 1 ? 15 : 14);
            line.color = i % 2 == 0 && i > 0 ? Color.white : Gold;
            line.alignment = TextAnchor.UpperLeft;
            Place((RectTransform)line.transform, 20, y[i], 310, i % 2 == 0 && i > 0 ? 50 : 25);
            overviewLines[i] = line;
        }
        conversationPanel = new GameObject("ConversationPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        conversationPanel.transform.SetParent(summaryPage, false);
        Place((RectTransform)conversationPanel.transform, 20, -632, 350, 145);
        Image conversationImage = conversationPanel.GetComponent<Image>();
        conversationImage.sprite = source.sprite;
        conversationImage.color = source.color;
        SetSliced(conversationImage, 2);
        conversationImage.raycastTarget = false;
        Text conversationTitle = Instantiate(titleTemplate, conversationPanel.transform);
        conversationTitle.name = "ConversationTitle";
        conversationTitle.text = "PONTO PARA CONVERSAR";
        conversationTitle.fontSize = 16;
        conversationTitle.color = Gold;
        conversationTitle.alignment = TextAnchor.UpperLeft;
        Place(conversationTitle.rectTransform, 20, -20, 310, 25);
        conversationText = Instantiate(titleTemplate, conversationPanel.transform);
        conversationText.name = "ConversationText";
        conversationText.text = "";
        conversationText.fontSize = 14;
        conversationText.color = Color.white;
        conversationText.alignment = TextAnchor.UpperLeft;
        Place(conversationText.rectTransform, 20, -53, 310, 75);
        return result;
    }

    private View CreateView(string name, GameObject card, GameObject comparison, string activity, bool energy)
    {
        GameObject viewportObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(summaryPage, false);
        RectTransform viewport = (RectTransform)viewportObject.transform;
        Place(viewport, 20, energy ? -340 : -310, 350, energy ? 488 : 518);
        Image hitArea = viewportObject.GetComponent<Image>();
        hitArea.color = new Color(0, 0, 0, 0);
        hitArea.raycastTarget = true;
        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);
        RectTransform content = (RectTransform)contentObject.transform;
        Place(content, 0, 0, 350, 1050);
        card.transform.SetParent(content, false);
        card.name = energy ? "EnergyCard" : name + "Card";
        Place((RectTransform)card.transform, 0, 0, 350, 486);
        comparison.transform.SetParent(content, false);
        comparison.name = name + "Comparison";
        Place((RectTransform)comparison.transform, 0, -505, 350, 522);
        ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;
        return new View { Root = viewportObject, Card = card, Comparison = comparison, Content = content, Scroll = scroll, Activity = activity };
    }

    private void ConfigureGameView(View view, string title, string first, string second)
    {
        Find(view.Card.transform, "EnergyTitle").GetComponent<Text>().text = title;
        Find(view.Card.transform, "ShiftDropdownButton").gameObject.SetActive(false);
        Find(view.Card.transform, "ShiftOptions").gameObject.SetActive(false);
        Find(view.Card.transform, "Category0/Name").GetComponent<Text>().text = first;
        Find(view.Card.transform, "Category1/Name").GetComponent<Text>().text = second;
        bool rush = view.Activity == "rush_balance";
        Find(view.Card.transform, "Category2/Name").GetComponent<Text>().text = rush ? "Impacientes perdidos" : "Equipe nas especialidades";
        Find(view.Card.transform, "Category3/Name").GetComponent<Text>().text = rush ? "Mudanças na fila" : "Pausas reorganizadas";
        for (int category = 2; category < 4; category++)
        {
            Transform row = Find(view.Card.transform, "Category" + category);
            Find(row, "ColorDot").gameObject.SetActive(false);
            Find(row, "CategorySelected").gameObject.SetActive(false);
            row.GetComponent<Button>().enabled = false;
        }
        Find(view.Card.transform, "DonutChart").GetComponent<DashboardDonutGraphic>().SetPalette(OutcomePalette);
        for (int turn = 0; turn < 3; turn++)
        {
            Transform bar = Find(view.Comparison.transform, "TurnCard" + turn + "/SegmentBar");
            Find(bar, "Segment0").GetComponent<Image>().color = Gold;
            Find(bar, "Segment1").GetComponent<Image>().color = OutcomePalette[1];
        }
        for (int i = 0; i < 2; i++)
        {
            int category = i;
            Find(view.Card.transform, "Category" + i).GetComponent<Button>().onClick.AddListener(() =>
            {
                selectedCategory = selectedCategory == category ? -1 : category;
                Refresh();
            });
        }
        Find(view.Comparison.transform, "ShiftsTitle").GetComponent<Text>().text = "Comparar turnos";
        foreach (int turn in new[] { 0, 1, 2 })
        {
            int selected = turn + 1;
            Find(view.Comparison.transform, "TurnCard" + turn).GetComponent<Button>().onClick.AddListener(() => SelectShift(selected));
        }
    }

    private void BuildGlobalFilters()
    {
        Transform card = views[0].Card.transform;
        shiftButton = Find(card, "ShiftDropdownButton").GetComponent<Button>();
        shiftOptions = Find(card, "ShiftOptions");
        shiftText = Find(shiftButton.transform, "Label").GetComponent<Text>();
        shiftButton.transform.SetParent(layout, false);
        shiftOptions.SetParent(layout, false);

        PlaceFilterButton((RectTransform)periodButton.transform, 105, -272);
        PlaceFilterButton((RectTransform)shiftButton.transform, 285, -272);
        SetSliced(periodButton.GetComponent<Image>(), 3);
        SetSliced(shiftButton.GetComponent<Image>(), 3);
        Place((RectTransform)periodOptions, 20, -294, 170, 136);
        Place((RectTransform)shiftOptions, 200, -294, 170, 178);
        SetSliced(periodOptions.GetComponent<Image>(), 3);
        SetSliced(shiftOptions.GetComponent<Image>(), 3);
        ResizeOptions(periodOptions, 170);
        ResizeOptions(shiftOptions, 170);
        periodOptions.SetAsLastSibling();
        shiftOptions.SetAsLastSibling();

        Text template = Find(card, "EnergyTitle").GetComponent<Text>();
        AddFilterLabel(template, "PeriodFilterLabel", "Período", 20);
        AddFilterLabel(template, "ShiftFilterLabel", "Turno", 200);
        Place(summaryCounts.rectTransform, 20, -307, 350, 25);
        legacy.SyncExternalDropdownPositions();
    }

    private void AddFilterLabel(Text template, string name, string caption, float x)
    {
        Text label = Instantiate(template, layout);
        label.name = name;
        label.text = caption;
        label.fontSize = 12;
        label.color = new Color32(0xcc, 0xcc, 0xcc, 0xff);
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;
        Place(label.rectTransform, x, -233, 170, 18);
    }

    private static void PlaceFilterButton(RectTransform rect, float x, float y)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(170, 38);
        rect.localScale = Vector3.one;
        Text label = rect.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.fontSize = 13;
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.sizeDelta = Vector2.zero;
        }
    }

    private static void ResizeOptions(Transform options, float width)
    {
        foreach (Transform child in options)
        {
            RectTransform rect = child as RectTransform;
            if (rect == null) continue;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.anchoredPosition = new Vector2(width * 0.5f, rect.anchoredPosition.y);
            SetSliced(child.GetComponent<Image>(), 3);
            Text label = child.GetComponentInChildren<Text>(true);
            if (label != null)
                label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 10);
        }
    }

    private static void SetSliced(Image image, float sourceScale)
    {
        if (image == null || image.sprite == null) return;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = sourceScale;
    }

    private void WireEnergyFilters()
    {
        Transform card = views[0].Card.transform;
        for (int i = 0; i < 4; i++)
        {
            int category = i;
            Find(card, "Category" + i).GetComponent<Button>().onClick.AddListener(() =>
            {
                selectedCategory = selectedCategory == category ? -1 : category;
                Refresh();
            });
            int turn = i;
            Find(shiftOptions, "ShiftOption" + i).GetComponent<Button>().onClick.AddListener(() =>
            {
                selectedShift = turn;
                selectedCategory = -1;
                Refresh();
            });
        }
        for (int i = 0; i < 3; i++)
        {
            int turn = i + 1;
            Find(views[0].Comparison.transform, "TurnCard" + i).GetComponent<Button>().onClick.AddListener(() => SelectShift(turn));
        }
    }

    private void SelectShift(int shift)
    {
        selectedShift = Mathf.Clamp(shift, 0, TurnNames.Length - 1);
        selectedCategory = -1;
        legacy.SelectTurn(shift);
        Refresh();
    }

    private void ShowTab(int tab)
    {
        if (tab < 0 || tab >= nav.Length) return;
        if (tabsInitialized && selectedTab == tab) return;
        int previous = selectedTab;
        bool animate = tabsInitialized;
        tabTransition?.Kill();
        tabTransition = null;
        pagesContainer.anchoredPosition = pagesBasePosition;
        pagesGroup.alpha = 1f;
        pagesGroup.blocksRaycasts = false;
        for (int i = 0; i < tabSections.Length; i++)
        {
            RectTransform rect = (RectTransform)tabSections[i].transform;
            CanvasGroup group = tabSections[i].GetComponent<CanvasGroup>();
            rect.anchoredPosition = Vector2.zero;
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
            tabSections[i].SetActive(i == tab);
        }

        selectedTab = tab;
        selectedCategory = -1;
        legacy.CloseExternalFilters();
        bool showFilters = tab != 4;
        periodButton.gameObject.SetActive(showFilters);
        shiftButton.gameObject.SetActive(showFilters);
        Find(layout, "PeriodFilterLabel").gameObject.SetActive(showFilters);
        Find(layout, "ShiftFilterLabel").gameObject.SetActive(showFilters);
        legacy.ShowPageImmediate(tab == 4 ? 2 : 0);

        for (int i = 0; i < nav.Length; i++)
        {
            bool active = i == tab;
            nav[i].GetComponent<Image>().color = active ? Gold : Inactive;
            Text label = nav[i].GetComponentInChildren<Text>(true);
            label.color = active ? new Color32(0x25, 0x20, 0x15, 0xff) : new Color32(0xcc, 0xcc, 0xcc, 0xff);
        }
        Refresh();
        tabsInitialized = true;

        if (tab < tabSections.Length)
        {
            CanvasGroup visible = tabSections[tab].GetComponent<CanvasGroup>();
            visible.interactable = true;
            visible.blocksRaycasts = true;
        }
        if (animate) AnimateTabEntry(tab > previous ? 1f : -1f);
        else pagesGroup.blocksRaycasts = true;
    }

    private void AnimateTabEntry(float direction)
    {
        pagesContainer.anchoredPosition = pagesBasePosition + Vector2.right * (TabTransitionDistance * direction);
        pagesGroup.alpha = 0.55f;
        tabTransition = DOTween.Sequence().SetUpdate(UpdateType.Normal, UIMotionDefaults.UseUnscaledTime);
        tabTransition.Append(pagesContainer.DOAnchorPos(pagesBasePosition, TabTransitionDuration).SetEase(Ease.OutCubic));
        tabTransition.Join(pagesGroup.DOFade(1f, TabTransitionDuration).SetEase(Ease.OutCubic));
        tabTransition.OnComplete(() => {
            pagesGroup.blocksRaycasts = true;
            tabTransition = null;
        });
    }

    private void Refresh()
    {
        periodText.text = demonstration ? "Demonstração" : MonthName(DateTime.Now.AddMonths(-monthOffset));
        storeText.text = demonstration || cloud?.IsReady == true ? "Minha loja" : "Neste aparelho";
        shiftText.text = TurnNames[selectedShift];
        DateTime month = DateTime.Now.AddMonths(-monthOffset);
        List<EventModel> sessions = DashboardSessionData.Read(demonstration);
        DashboardSessionData.Snapshot energy = GetSnapshot(sessions, "energy_station", month, selectedShift);
        DashboardSessionData.Snapshot rush = GetSnapshot(sessions, "rush_balance", month, selectedShift);
        DashboardSessionData.Snapshot combo = GetSnapshot(sessions, "combo_crew", month, selectedShift);
        DashboardSessionData.Snapshot energyBefore = GetSnapshot(sessions, "energy_station", month.AddMonths(-1), selectedShift);
        DashboardSessionData.Snapshot rushBefore = GetSnapshot(sessions, "rush_balance", month.AddMonths(-1), selectedShift);
        DashboardSessionData.Snapshot comboBefore = GetSnapshot(sessions, "combo_crew", month.AddMonths(-1), selectedShift);
        RefreshOverview(energy, rush, combo);
        summaryCounts.text = energy.Completed + " sessões concluídas · " + energy.TotalChoices + " escolhas " + (demonstration ? "na demonstração" : cloud?.IsReady == true ? "na unidade" : "neste aparelho");
        RefreshEnergy(energy, energyBefore, sessions, month);
        RefreshOutcome(views[1], rush, rushBefore, sessions, month, "pedidos");
        RefreshOutcome(views[2], combo, comboBefore, sessions, month, "pedidos");
    }

    private DashboardSessionData.Snapshot GetSnapshot(List<EventModel> sessions, string activity, DateTime month, int shift)
    {
        return !demonstration && cloud?.IsReady == true
            ? cloud.Get(activity, month, shift)
            : DashboardSessionData.Aggregate(sessions, activity, month, shift);
    }

    private void RefreshOverview(DashboardSessionData.Snapshot energy, DashboardSessionData.Snapshot rush, DashboardSessionData.Snapshot combo)
    {
        if (overviewLines == null) return;
        string choice = energy.TotalChoices == 0 ? "Ainda não há escolhas neste período." :
            EnergyNames[MaxIndex(energy.Choices)] + " foi a necessidade mais escolhida.";
        string pressure = rush.TotalOrders == 0 ? "Ainda não há pedidos neste período." :
            Percent(rush.Delivered, rush.TotalOrders) + "% dos pedidos foram atendidos.";
        if (rush.ImpatientOrders > 0)
            pressure += " " + Percent(rush.ImpatientMissed, rush.ImpatientOrders) + "% dos pedidos impacientes foram perdidos.";
        string coordination = combo.Breaks == 0 ? "Ainda não há pausas simuladas neste período." :
            "A equipe foi reorganizada em " + combo.BreakReorganizations + " de " + combo.Breaks + " pausas simuladas.";
        overviewLines[2].text = choice;
        overviewLines[4].text = pressure;
        if (combo.SpecialistOpportunities > 0)
            coordination += " " + combo.SpecialistPlacements + " de " + combo.SpecialistOpportunities + " especialistas em suas estações.";
        overviewLines[6].text = coordination;
        if (energy.TotalChoices + rush.TotalOrders + combo.TotalOrders == 0)
            conversationText.text = "Conclua uma atividade neste período para ver uma sugestão de conversa.";
        else if (rush.TotalOrders > 0 && rush.Missed > 0)
            conversationText.text = "Como a equipe decide a ordem dos pedidos nos momentos de maior movimento?";
        else if (combo.Breaks > 0)
            conversationText.text = "Como redistribuir as estações quando alguém precisa sair para uma pausa?";
        else
            conversationText.text = "Como garantir que as necessidades da equipe sejam percebidas durante o turno?";
    }

    private void RefreshEnergy(DashboardSessionData.Snapshot current, DashboardSessionData.Snapshot before, List<EventModel> sessions, DateTime month)
    {
        SetCardValues(views[0], current.Choices, before.Choices, EnergyNames, current.TotalChoices, "escolhas");
        RefreshComparison(views[0], sessions, month, "energy_station", true);
    }

    private void RefreshOutcome(View view, DashboardSessionData.Snapshot current, DashboardSessionData.Snapshot before,
        List<EventModel> sessions, DateTime month, string unit)
    {
        int[] values = { current.Delivered, current.Missed, 0, 0 };
        int[] previous = { before.Delivered, before.Missed, 0, 0 };
        string[] names = { "Atendidos", "Perdidos", "", "" };
        SetCardValues(view, values, previous, names, current.TotalOrders, unit);
        bool rush = view.Activity == "rush_balance";
        Find(view.Card.transform, "Category2/Percent").GetComponent<Text>().text = rush
            ? (current.ImpatientOrders > 0 ? current.ImpatientMissed + "/" + current.ImpatientOrders : "—")
            : (current.SpecialistOpportunities > 0 ? current.SpecialistPlacements + "/" + current.SpecialistOpportunities : "—");
        Find(view.Card.transform, "Category3/Percent").GetComponent<Text>().text = rush
            ? current.Reorders.ToString()
            : (current.Breaks > 0 ? current.BreakReorganizations + "/" + current.Breaks : "—");
        RefreshComparison(view, sessions, month, view.Activity, false);
    }

    private void SetCardValues(View view, int[] values, int[] before, string[] names, int total, string unit)
    {
        Transform card = view.Card.transform;
        DashboardDonutGraphic graphic = Find(card, "DonutChart").GetComponent<DashboardDonutGraphic>();
        AnimateDonut(view, graphic, values, total);
        for (int i = 0; i < 4; i++)
        {
            Text percent = Find(card, "Category" + i + "/Percent").GetComponent<Text>();
            percent.text = total == 0 ? "—" : Percent(values[i], total) + "%";
            Image highlight = Find(card, "Category" + i + "/CategorySelected").GetComponent<Image>();
            highlight.enabled = i == selectedCategory && i < (card == views[0].Card.transform ? 4 : 2);
        }
        Text large = Find(card, "DonutChart/CenterLarge").GetComponent<Text>();
        Text middle = Find(card, "DonutChart/CenterMiddle").GetComponent<Text>();
        Text small = Find(card, "DonutChart/CenterSmall").GetComponent<Text>();
        if (selectedCategory < 0 || selectedCategory >= names.Length || string.IsNullOrEmpty(names[selectedCategory]))
        {
            large.text = total == 0 ? "0" : total.ToString();
            large.fontSize = 40;
            middle.text = unit;
            small.text = TurnNames[selectedShift];
            large.color = Color.white;
            large.rectTransform.anchoredPosition = new Vector2(35, -85);
            middle.rectTransform.anchoredPosition = new Vector2(25, -136);
            small.rectTransform.anchoredPosition = new Vector2(25, -169);
            small.rectTransform.sizeDelta = new Vector2(200, 53);
            small.fontSize = 13;
        }
        else
        {
            int previousTotal = before[0] + before[1] + before[2] + before[3];
            float nowShare = total == 0 ? 0 : values[selectedCategory] * 100f / total;
            float oldShare = previousTotal == 0 ? 0 : before[selectedCategory] * 100f / previousTotal;
            float difference = nowShare - oldShare;
            large.text = previousTotal == 0 ? "—" : (difference > 0 ? "+" : "") + difference.ToString("0.0", PtBr) + " p.p.";
            large.fontSize = 28;
            middle.text = names[selectedCategory];
            large.color = card == views[0].Card.transform ? EnergyPalette[selectedCategory] : OutcomePalette[selectedCategory];
            large.rectTransform.anchoredPosition = new Vector2(35, -100);
            middle.rectTransform.anchoredPosition = new Vector2(25, -65);
            small.rectTransform.anchoredPosition = new Vector2(50, -153);
            small.rectTransform.sizeDelta = new Vector2(150, 58);
            small.fontSize = 12;
            small.text = previousTotal == 0 ? "Sem dados no mês anterior" :
                oldShare.ToString("0.0", PtBr) + "% → " + nowShare.ToString("0.0", PtBr) + "%\nvs. mês anterior";
        }
    }

    private static void AnimateDonut(View view, DashboardDonutGraphic graphic, int[] values, int total)
    {
        float[] target = new float[4];
        float change = 0f;
        for (int i = 0; i < 4; i++)
        {
            target[i] = total == 0 ? 0f : values[i] / (float)total;
            change += Mathf.Abs(target[i] - view.RenderedShares[i]);
        }
        if (view.DonutInitialized && change < 0.0001f) return;
        view.DonutTween?.Kill();
        if (!view.DonutInitialized)
        {
            Array.Copy(target, view.RenderedShares, 4);
            graphic.SetShares(view.RenderedShares);
            view.DonutInitialized = true;
            return;
        }

        float[] from = (float[])view.RenderedShares.Clone();
        view.DonutTween = DOTween.To(() => 0f, progress =>
        {
            for (int i = 0; i < 4; i++)
                view.RenderedShares[i] = Mathf.Lerp(from[i], target[i], progress);
            graphic.SetShares(view.RenderedShares);
        }, 1f, 0.42f).SetEase(Ease.InOutCubic).SetUpdate(true)
        .OnComplete(() =>
        {
            Array.Copy(target, view.RenderedShares, 4);
            graphic.SetShares(view.RenderedShares);
            view.DonutTween = null;
        });
    }

    private void RefreshComparison(View view, List<EventModel> sessions, DateTime month, string activity, bool energy)
    {
        bool show = selectedShift == 0;
        view.Comparison.SetActive(show);
        view.Content.sizeDelta = new Vector2(350, show ? 1040 : 495);
        if (!show) { view.Scroll.verticalNormalizedPosition = 1; return; }
        for (int i = 0; i < 3; i++)
        {
            DashboardSessionData.Snapshot snapshot = GetSnapshot(sessions, activity, month, i + 1);
            int[] values = energy ? snapshot.Choices : new[] { snapshot.Delivered, snapshot.Missed, 0, 0 };
            int sum = energy ? snapshot.TotalChoices : snapshot.TotalOrders;
            Transform turnCard = Find(view.Comparison.transform, "TurnCard" + i);
            Find(turnCard, "ChoiceCount").GetComponent<Text>().text = sum + (energy ? " escolhas" : " pedidos");
            float x = 0;
            for (int j = 0; j < 4; j++)
            {
                Transform segment = Find(turnCard, "SegmentBar/Segment" + j);
                segment.gameObject.SetActive(energy || j < 2);
                float width = sum == 0 ? 0 : 286f * values[j] / sum;
                ((RectTransform)segment).anchoredPosition = new Vector2(x, 0);
                ((RectTransform)segment).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                Text percent = Find(turnCard, "SegmentPercent" + j).GetComponent<Text>();
                percent.text = (energy || j < 2) && sum > 0 && values[j] > 0
                    ? Percent(values[j], sum) + "%" : "";
                RectTransform percentRect = percent.rectTransform;
                float barX = ((RectTransform)segment.parent).anchoredPosition.x;
                percentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                percentRect.anchoredPosition = new Vector2(barX + x, percentRect.anchoredPosition.y);
                percent.alignment = TextAnchor.MiddleCenter;
                percent.fontSize = Mathf.Clamp(Mathf.FloorToInt(width * 0.55f), 9, 12);
                x += width;
            }
        }
    }

    private static string MonthName(DateTime date)
    {
        string text = date.ToString("MMMM yyyy", PtBr);
        return char.ToUpper(text[0], PtBr) + text.Substring(1);
    }
    private static int Percent(int value, int total) => total == 0 ? 0 : Mathf.RoundToInt(value * 100f / total);
    private static int MaxIndex(int[] values)
    {
        int index = 0;
        for (int i = 1; i < values.Length; i++) if (values[i] > values[index]) index = i;
        return index;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
}
