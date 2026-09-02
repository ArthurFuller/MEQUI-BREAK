#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Instala uma vez o layout manual do tutorial. Depois de salvo na cena, nenhum
/// objeto tem posição ou tamanho recalculado em runtime.
/// </summary>
[InitializeOnLoad]
public static class FirstRunGuideHierarchyInstaller
{
    private const string StatePath = "ProjectSettings/FirstRunGuideHierarchy.state";
    private const string MinigameCardPrefabPath = "Assets/Prefabs/UI/MinigameCard.prefab";
    private const string Version = "5";
    private const int GuideSortingOrder = 10000;
    private const int GuideForegroundSortingOrder = GuideSortingOrder + 3;

    private static readonly SceneSetup[] SceneSetups =
    {
        new SceneSetup(
            "Assets/Scenes/Hub/HUB.unity",
            0,
            new Vector2(-106f, 766f),
            new Vector2(250f, 125f),
            new Vector2(0f, 500f)),
        new SceneSetup(
            "Assets/Scenes/Minigames/Minigames.unity",
            1,
            Vector2.zero,
            new Vector2(285f, 285f),
            new Vector2(0f, -610f)),
        new SceneSetup(
            "Assets/Scenes/EnergyStation/EnergyStation.unity",
            2,
            new Vector2(-270f, 0f),
            new Vector2(160f, 152f),
            new Vector2(0f, 610f))
    };

    static FirstRunGuideHierarchyInstaller()
    {
        EditorApplication.delayCall += TryAutoInstall;
    }

    [MenuItem("Mequi Break/Tutorial/Reinstall Manual Layout")]
    public static void ForceInstall()
    {
        if (File.Exists(StatePath))
            File.Delete(StatePath);
        InstallAll();
    }

    private static void TryAutoInstall()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryAutoInstall;
            return;
        }

        if (File.Exists(StatePath) && File.ReadAllText(StatePath).Trim() == Version)
            return;

        InstallAll();
    }

    private static void InstallAll()
    {
        string previousScene = SceneManager.GetActiveScene().path;
        try
        {
            for (int i = 0; i < SceneSetups.Length; i++)
                InstallScene(SceneSetups[i]);

            AssetDatabase.SaveAssets();
            File.WriteAllText(StatePath, Version);

            if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

            Debug.Log("Mequi Break: layout manual do tutorial instalado nas cenas.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError(
                "Mequi Break: a instalação do tutorial não terminou. " +
                "Use Mequi Break/Tutorial/Reinstall Manual Layout após corrigir o erro.");
        }
    }

    private static void InstallScene(SceneSetup setup)
    {
        Scene scene = EditorSceneManager.OpenScene(setup.Path, OpenSceneMode.Single);
        Transform canvasTransform = Find(scene, "Canvas");
        if (canvasTransform == null)
            throw new InvalidOperationException($"Canvas não encontrado em {setup.Path}");

        // Remove a estrutura antiga com os quatro bloqueios. A versão nova é
        // propositalmente simples e totalmente manipulável no RectTransform.
        Transform oldRoot = canvasTransform.Find("TutorialRoot");
        if (oldRoot != null)
            UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

        GameObject tutorialObject = CreateUI(canvasTransform, "TutorialRoot");
        RectTransform tutorialRoot = tutorialObject.GetComponent<RectTransform>();
        Stretch(tutorialRoot);
        tutorialObject.transform.SetAsLastSibling();

        Canvas guideCanvas = tutorialObject.AddComponent<Canvas>();
        guideCanvas.overrideSorting = true;
        guideCanvas.sortingOrder = GuideSortingOrder;
        tutorialObject.AddComponent<GraphicRaycaster>();

        // O próprio TutorialRoot é o painel escuro de tela cheia. Ele bloqueia
        // as camadas inferiores; somente os alvos elevados e o botão Pular
        // continuam interativos.
        Image overlay = tutorialObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);
        overlay.raycastTarget = true;

        GameObject panelObject = CreateUI(tutorialRoot, "Mensagem");
        Canvas panelCanvas = panelObject.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = GuideForegroundSortingOrder;
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.075f, 0.075f, 0.085f, 0.97f);
        panelImage.raycastTarget = false;
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        Center(panel, new Vector2(880f, 270f), setup.MessagePosition);
        CanvasGroup panelGroup = panelObject.AddComponent<CanvasGroup>();

        TMP_Text progress = CreateText(
            panel,
            "Progresso",
            $"GUIA {setup.Step + 1} DE 3",
            25f,
            new Color(1f, 0.78f, 0f, 1f),
            TextAlignmentOptions.Left);
        Center(progress.rectTransform, new Vector2(740f, 42f), new Vector2(0f, 82f));
        progress.fontStyle = FontStyles.Bold;

        TMP_Text message = CreateText(
            panel,
            "Texto",
            GetDefaultMessage(setup.Step),
            36f,
            Color.white,
            TextAlignmentOptions.MidlineLeft);
        Center(message.rectTransform, new Vector2(740f, 130f), new Vector2(0f, -18f));
        message.textWrappingMode = TextWrappingModes.Normal;

        GameObject skipObject = CreateUI(tutorialRoot, "Pular");
        Canvas skipCanvas = skipObject.AddComponent<Canvas>();
        skipCanvas.overrideSorting = true;
        skipCanvas.sortingOrder = GuideForegroundSortingOrder;
        skipObject.AddComponent<GraphicRaycaster>();
        Image skipImage = skipObject.AddComponent<Image>();
        skipImage.color = new Color(0.075f, 0.075f, 0.085f, 0.96f);
        skipImage.raycastTarget = true;
        Button skipButton = skipObject.AddComponent<Button>();
        skipButton.targetGraphic = skipImage;
        RectTransform skipRect = skipObject.GetComponent<RectTransform>();
        Center(skipRect, new Vector2(250f, 78f), new Vector2(0f, -820f));
        CanvasGroup skipGroup = skipObject.AddComponent<CanvasGroup>();

        TMP_Text skipText = CreateText(
            skipRect,
            "Texto",
            "PULAR GUIA",
            27f,
            Color.white,
            TextAlignmentOptions.Center);
        Stretch(skipText.rectTransform, new Vector2(18f, 8f));
        skipText.fontStyle = FontStyles.Bold;

        Button targetButton = null;
        EnergyStationController station = null;
        Transform highlightParent;
        string highlightName;
        MinigameCardView minigameCard = null;
        if (setup.Step == 0)
        {
            Transform targetTransform = Find(scene, "Canvas/SafeArea/QuickAction/GamesButton");
            targetButton = targetTransform != null ? targetTransform.GetComponent<Button>() : null;
            if (targetButton == null)
                throw new InvalidOperationException("GamesButton não encontrado na HUB.");

            // O botão fica em uma camada imediatamente acima do tutorial. Isso
            // é configurado na cena, não em runtime, e mantém o clique ativo.
            ConfigureTargetLayer(targetTransform);
            highlightParent = targetTransform.parent;
            highlightName = "DestaqueTutorial";
        }
        else if (setup.Step == 1)
        {
            Transform minigameList = Find(scene, "Canvas/Safe Area/MinigameList");
            if (minigameList == null)
                throw new InvalidOperationException("MinigameList não encontrado.");

            ConfigureTargetLayer(minigameList);
            MinigameSelectionController selection = FindSceneComponent<MinigameSelectionController>(scene);
            MinigameCardView[] cards = PrepareMinigameCards(selection, minigameList);
            if (cards.Length == 0)
                throw new InvalidOperationException("Nenhum card foi configurado na cena Minigames.");

            minigameCard = cards[0];
            highlightParent = minigameList;
            highlightName = "DestaqueTutorial";
        }
        else
        {
            station = FindSceneComponent<EnergyStationController>(scene);
            if (station == null)
                throw new InvalidOperationException("EnergyStationController não encontrado.");

            Transform interactionArea = Find(scene, "Canvas/SafeArea/InteractionArea");
            Transform interactionObjects = Find(
                scene,
                "Canvas/SafeArea/InteractionArea/InteractionObjects");
            Transform avatarArea = Find(scene, "Canvas/SafeArea/AvatarArea");
            if (interactionArea == null || interactionObjects == null || avatarArea == null)
                throw new InvalidOperationException("Áreas interativas da Energy Station não encontradas.");

            ConfigureTargetLayer(interactionArea);
            ConfigureTargetLayer(avatarArea);
            highlightParent = interactionObjects;
            highlightName = "DestaqueHydration";

            Transform legacyHighlight = interactionObjects.Find("DestaqueTutorial");
            if (legacyHighlight != null)
                UnityEngine.Object.DestroyImmediate(legacyHighlight.gameObject);
        }

        Image highlight = CreateHighlight(
            highlightParent,
            highlightName,
            setup.HighlightSize,
            setup.HighlightPosition);
        GameObject highlightObject = highlight.gameObject;
        Image secondaryHighlight = null;

        if (setup.Step == 2)
        {
            secondaryHighlight = CreateHighlight(
                highlightParent,
                "DestaqueBreak",
                setup.HighlightSize,
                new Vector2(-90f, 0f));
        }

        if (setup.Step == 0)
        {
            ConfigureStandaloneHighlightLayer(highlightObject);
        }
        else
        {
            // O destaque compartilha exatamente o mesmo espaço de coordenadas
            // e o mesmo Canvas do alvo, mas permanece atrás dele na Hierarchy.
            if (setup.Step == 1)
            {
                LayoutElement layout = highlightObject.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;

                RectTransform listRect = highlightParent as RectTransform;
                if (listRect != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);

                RectTransform cardRect = minigameCard.transform as RectTransform;
                if (cardRect != null)
                {
                    RectTransform highlightRect = highlight.rectTransform;
                    highlightRect.anchorMin = cardRect.anchorMin;
                    highlightRect.anchorMax = cardRect.anchorMax;
                    highlightRect.pivot = cardRect.pivot;
                    highlightRect.anchoredPosition = cardRect.anchoredPosition;
                    highlightRect.sizeDelta = cardRect.sizeDelta + new Vector2(35f, 35f);
                }
            }

            highlightObject.transform.SetAsFirstSibling();
            if (secondaryHighlight != null)
                secondaryHighlight.transform.SetSiblingIndex(1);
        }

        FirstRunGuideSceneView view = tutorialObject.AddComponent<FirstRunGuideSceneView>();
        view.ConfigureReferences(
            setup.Step,
            guideCanvas,
            tutorialRoot,
            highlight,
            secondaryHighlight,
            panel,
            panelGroup,
            skipGroup,
            progress,
            message,
            skipButton,
            targetButton,
            station);

        // Visível no modo de edição: o usuário pode selecionar e mover cada
        // RectTransform. O Awake do componente oculta o Canvas antes do jogo.
        guideCanvas.enabled = true;
        highlightObject.SetActive(true);

        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static MinigameCardView[] PrepareMinigameCards(
        MinigameSelectionController selection,
        Transform fallbackContentRoot)
    {
        if (selection == null)
            throw new InvalidOperationException("MinigameSelectionController não encontrado.");

        MinigameDefinition[] definitions = selection.EditorDefinitions;
        GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(
            MinigameCardPrefabPath);
        MinigameCardView prefab = prefabObject != null
            ? prefabObject.GetComponent<MinigameCardView>()
            : null;
        Transform contentRoot = fallbackContentRoot;
        SceneLoader sceneLoader = selection.EditorSceneLoader;

        if (definitions == null || definitions.Length == 0 || prefab == null || contentRoot == null)
            throw new InvalidOperationException("Configuração de cards de minigame incompleta.");

        MinigameCardView[] oldCards = contentRoot.GetComponentsInChildren<MinigameCardView>(true);
        for (int i = 0; i < oldCards.Length; i++)
        {
            if (oldCards[i] != null)
                UnityEngine.Object.DestroyImmediate(oldCards[i].gameObject);
        }

        List<MinigameCardView> cards = new List<MinigameCardView>();
        for (int i = 0; i < definitions.Length; i++)
        {
            MinigameDefinition definition = definitions[i];
            if (definition == null)
                continue;

            GameObject cardObject = PrefabUtility.InstantiatePrefab(
                prefab.gameObject,
                contentRoot.gameObject.scene) as GameObject;
            if (cardObject == null)
                throw new InvalidOperationException("Não foi possível instanciar o prefab MinigameCard no Editor.");

            cardObject.name = $"{definition.DisplayName}Card";
            cardObject.transform.SetParent(contentRoot, false);

            MinigameCardView card = cardObject.GetComponent<MinigameCardView>();
            card.ConfigureInEditor(definition, sceneLoader);
            cards.Add(card);

            Component[] components = cardObject.GetComponentsInChildren<Component>(true);
            for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                Component component = components[componentIndex];
                if (component == null)
                    continue;

                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        MinigameCardView[] result = cards.ToArray();
        selection.ConfigureSceneCards(result);
        EditorUtility.SetDirty(selection);
        return result;
    }

    private static Image CreateHighlight(
        Transform parent,
        string name,
        Vector2 size,
        Vector2 position)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        GameObject highlightObject = CreateUI(parent, name);
        Image highlight = highlightObject.AddComponent<Image>();
        highlight.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        highlight.type = Image.Type.Sliced;
        highlight.color = new Color(1f, 0.78f, 0f, 0.38f);
        highlight.raycastTarget = false;
        Center(highlight.rectTransform, size, position);
        return highlight;
    }

    private static void ConfigureStandaloneHighlightLayer(GameObject highlightObject)
    {
        Canvas highlightCanvas = GetOrAdd<Canvas>(highlightObject);
        highlightCanvas.overrideSorting = true;
        highlightCanvas.sortingOrder = GuideSortingOrder + 1;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUI(parent, name);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static string GetDefaultMessage(int step)
    {
        return step switch
        {
            0 => "Toque aqui para conhecer os minigames.",
            1 => "Aqui ficam os minigames. Toque em Energy Station.",
            _ => "Arraste um card até o avatar para indicar uma pausa."
        };
    }

    private static Transform Find(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != parts[0])
                continue;

            Transform current = roots[i].transform;
            for (int part = 1; part < parts.Length && current != null; part++)
                current = current.Find(parts[part]);
            return current;
        }

        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }

    private static GameObject CreateUI(Transform parent, string name)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void ConfigureTargetLayer(Transform target)
    {
        Canvas targetCanvas = GetOrAdd<Canvas>(target.gameObject);
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = GuideSortingOrder + 2;
        GetOrAdd<GraphicRaycaster>(target.gameObject);
    }

    private static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = inset;
        rect.offsetMax = -inset;
        rect.localScale = Vector3.one;
    }

    private readonly struct SceneSetup
    {
        public SceneSetup(
            string path,
            int step,
            Vector2 highlightPosition,
            Vector2 highlightSize,
            Vector2 messagePosition)
        {
            Path = path;
            Step = step;
            HighlightPosition = highlightPosition;
            HighlightSize = highlightSize;
            MessagePosition = messagePosition;
        }

        public string Path { get; }
        public int Step { get; }
        public Vector2 HighlightPosition { get; }
        public Vector2 HighlightSize { get; }
        public Vector2 MessagePosition { get; }
    }
}
#endif
