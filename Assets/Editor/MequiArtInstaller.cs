#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Integração determinística e executada uma única vez para o pacote em Assets/Art.
/// Usa APIs do Editor para serializar as referências de sprites diretamente nas
/// cenas, sem depender de Resources ou Addressables.
/// </summary>
[InitializeOnLoad]
public static class MequiArtInstaller
{
    private const string StatePath = "ProjectSettings/MequiArtIntegration.state";
    private const string Version = "2";
    private const string DarkBackgroundPath = "Assets/Art/UI/PersonalizarPersonagem1/Tela 2.png";

    static MequiArtInstaller()
    {
        EditorApplication.delayCall += TryAutoInstall;
    }

    [MenuItem("Mequi Break/Art/Reinstall All Art")]
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
            ConfigureSpriteImports();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            InstallLogin();
            InstallHub();
            InstallCustomization();
            InstallEnergyStation();
            ConfigureCatalog();

            AssetDatabase.SaveAssets();
            File.WriteAllText(StatePath, Version);

            if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

            Debug.Log("Mequi Break: all Assets/Art images were integrated successfully.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError("Mequi Break art integration did not finish. Use Mequi Break/Art/Reinstall All Art after fixing the reported error.");
        }
    }

    private static void ConfigureSpriteImports()
    {
        // Os avatares antigos já possuem referências recortadas válidas.
        // Apenas as novas interfaces precisam de um único sprite por PNG.
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                continue;

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.filterMode != FilterMode.Bilinear
                || !Mathf.Approximately(importer.spritePixelsPerUnit, 100f);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100f;

            if (changed)
                importer.SaveAndReimport();
        }
    }

    // ---------------------------------------------------------------------
    // Login e abertura
    // ---------------------------------------------------------------------

    private static void InstallLogin()
    {
        Scene scene = Open("Assets/Scenes/Login/Login.unity");
        Transform canvas = Find(scene, "Canvas");
        Transform safe = Find(scene, "Canvas/SafeArea");
        ConfigureCanvas(canvas);
        ConfigureSafeArea(safe);

        Image background = ImageAt(scene, "Canvas/SafeArea/Background");
        SetFullScreen(background, S("Assets/Art/UI/TelaLogin/tela-login.png"), false);

        Disable(scene, "Canvas/SafeArea/WelcomeLabel");
        Disable(scene, "Canvas/SafeArea/NameFieldLabel");
        Disable(scene, "Canvas/SafeArea/StoreFieldLabel");

        Image form = CreateImage(safe, "ArtFormSection", S("Assets/Art/UI/TelaLogin/form-section.png"));
        Center(form.rectTransform, new Vector2(904f, 649f), new Vector2(0f, -90f));
        form.transform.SetAsFirstSibling();

        TMP_InputField name = Find(scene, "Canvas/SafeArea/NameInput")?.GetComponent<TMP_InputField>()
            ?? ComponentAt<TMP_InputField>(scene, "Canvas/SafeArea/EmployeeIDInput");
        TMP_InputField store = Find(scene, "Canvas/SafeArea/StoreInput")?.GetComponent<TMP_InputField>()
            ?? ComponentAt<TMP_InputField>(scene, "Canvas/SafeArea/PasswordInput");
        TMP_InputField shift = Find(scene, "Canvas/SafeArea/ShiftInput")?.GetComponent<TMP_InputField>();
        if (shift == null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(name.gameObject, safe, false);
            clone.name = "ShiftInput";
            shift = clone.GetComponent<TMP_InputField>();
        }

        name.gameObject.name = "NameInput";
        store.gameObject.name = "StoreInput";
        PrepareInput(name, new Vector2(0f, 110f), S("Assets/Art/UI/TelaLogin/placeholder 1.png"));
        PrepareInput(store, new Vector2(0f, -121f), S("Assets/Art/UI/TelaLogin/placeholder (1).png"));
        PrepareInput(shift, new Vector2(0f, -349f), S("Assets/Art/UI/TelaLogin/placeholder.png"));
        name.characterLimit = PlayerManager.DisplayNameMaxLength;
        store.characterLimit = PlayerManager.StoreIdMaxLength;
        shift.characterLimit = PlayerManager.ShiftMaxLength;

        Transform loginButtonTransform = Find(scene, "Canvas/SafeArea/LogginButton");
        Button loginButton = loginButtonTransform.GetComponent<Button>();
        SetButtonSprite(loginButton, S("Assets/Art/UI/TelaLogin/btn-entrar.png"));
        Center(loginButtonTransform as RectTransform, new Vector2(846f, 231f), new Vector2(0f, -590f));
        DisableDirectText(loginButtonTransform);

        TMP_Text error = ComponentAt<TMP_Text>(scene, "Canvas/SafeArea/ErrorMessage");
        Center(error.rectTransform, new Vector2(900f, 70f), new Vector2(0f, -735f));
        error.color = new Color(1f, 0.35f, 0.25f, 1f);
        error.fontSize = 30f;

        LoginController controller = Find(scene, "LoginController").GetComponent<LoginController>();
        SetObject(controller, "nameInput", name);
        SetObject(controller, "storeInput", store);
        SetObject(controller, "shiftInput", shift);
        SetObject(controller, "submitButton", loginButton);
        SetString(controller, "hubScene", "HUB");

        EnsurePersistentListener(loginButton, controller, nameof(LoginController.Login));

        GameObject splash = GetOrCreateUI(canvas, "ArtSplash");
        Stretch(splash.transform as RectTransform);
        Image splashImage = GetOrAdd<Image>(splash);
        splashImage.sprite = S("Assets/Art/UI/TelaInicial/Tela 1.png");
        splashImage.preserveAspect = false;
        splashImage.raycastTarget = true;
        splash.transform.SetAsLastSibling();

        GameObject splashButtonObject = GetOrCreateUI(splash.transform, "EnterButton");
        Button splashButton = GetOrAdd<Button>(splashButtonObject);
        Image splashButtonImage = GetOrAdd<Image>(splashButtonObject);
        splashButton.targetGraphic = splashButtonImage;
        splashButtonImage.sprite = S("Assets/Art/UI/TelaInicial/btn-entrar.png");
        splashButtonImage.preserveAspect = true;
        splashButtonImage.raycastTarget = true;
        Center(splashButtonObject.transform as RectTransform, new Vector2(846f, 231f), new Vector2(0f, -600f));

        SplashScreenController splashController = GetOrAdd<SplashScreenController>(canvas.gameObject);
        SetObject(splashController, "splashRoot", splash);
        SetObject(splashController, "enterButton", splashButton);

        Save(scene);
    }

    private static void PrepareInput(TMP_InputField input, Vector2 position, Sprite placeholderSprite)
    {
        RectTransform rect = input.transform as RectTransform;
        Center(rect, new Vector2(850f, 118f), position);

        Image rootImage = input.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.sprite = null;
            rootImage.color = Color.clear;
        }

        if (input.textComponent != null)
        {
            input.textComponent.color = new Color(0.96f, 0.96f, 0.96f, 1f);
            input.textComponent.fontSize = 31f;
            input.textComponent.margin = new Vector4(30f, 0f, 30f, 0f);
        }

        if (input.placeholder != null)
            input.placeholder.gameObject.SetActive(false);

        Transform parent = input.textViewport != null ? input.textViewport : input.transform;
        Image placeholder = CreateImage(parent, "ArtPlaceholder", placeholderSprite);
        placeholder.raycastTarget = false;
        placeholder.preserveAspect = true;
        Stretch(placeholder.rectTransform, new Vector2(30f, 18f), new Vector2(-30f, -18f));
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
    }

    // ---------------------------------------------------------------------
    // HUB
    // ---------------------------------------------------------------------

    private static void InstallHub()
    {
        Scene scene = Open("Assets/Scenes/Hub/HUB.unity");
        Transform canvas = Find(scene, "Canvas");
        Transform safe = Find(scene, "Canvas/SafeArea");
        ConfigureCanvas(canvas);
        ConfigureSafeArea(safe);

        SetFullScreen(ImageAt(scene, "Canvas/Background"), S("Assets/Art/UI/Home/lobby-mequi-break.png"), false);

        Transform playerCard = Find(scene, "Canvas/SafeArea/PlayerCard");
        Image playerCardImage = playerCard.GetComponent<Image>();
        if (playerCardImage != null)
            playerCardImage.color = Color.clear;
        Transform nameLabel = Find(scene, "Canvas/SafeArea/PlayerCard/NameLabel");
        if (nameLabel != null)
            nameLabel.gameObject.SetActive(true);
        Disable(scene, "Canvas/SafeArea/PlayerCard/RoleLabel");
        Disable(scene, "Canvas/SafeArea/PlayerCard/LevelLabel");

        Image avatar = ImageAt(scene, "Canvas/SafeArea/PlayerCard/Avatar");
        avatar.sprite = S("Assets/Art/UI/Home/cute-mascot.png");
        avatar.preserveAspect = true;
        Center(avatar.rectTransform, new Vector2(410f, 410f), new Vector2(0f, 245f));

        Transform header = Find(scene, "Canvas/SafeArea/Header");
        Disable(scene, "Canvas/SafeArea/Header/Logo(TROCAR PARA IMAGEM)");

        Button settings = ComponentAt<Button>(scene, "Canvas/SafeArea/Header/SettingsButton");
        SetButtonSprite(settings, S("Assets/Art/UI/Home/Settings Button.png"));
        AnchorTop(settings.transform as RectTransform, new Vector2(194f, 186f), new Vector2(-112f, -105f), false);
        DisableDirectText(settings.transform);

        Transform quick = Find(scene, "Canvas/SafeArea/QuickAction");
        Button customize = ComponentAt<Button>(scene, "Canvas/SafeArea/QuickAction/CustomizationButton");
        SetButtonSprite(customize, S("Assets/Art/UI/Home/edit-badge.png"));
        Center(customize.transform as RectTransform, new Vector2(104f, 104f), new Vector2(155f, 390f));
        DisableDirectText(customize.transform);
        Disable(scene, "Canvas/SafeArea/QuickAction/GamesButton");

        TMP_Text points = ComponentAt<TMP_Text>(scene, "Canvas/SafeArea/PlayerCard/PointsLabel");
        Image pbTitle = CreateImage(safe, "BreakPointsTitle", S("Assets/Art/UI/Home/Pontos Break.png"));
        AnchorTop(pbTitle.rectTransform, new Vector2(300f, 56f), new Vector2(210f, -205f), true);

        Image pbCounter = CreateImage(safe, "BreakPointsCounter", S("Assets/Art/UI/Home/Rectangle 1.png"));
        AnchorTop(pbCounter.rectTransform, new Vector2(748f, 157f), new Vector2(0f, -300f), true);
        points.transform.SetParent(pbCounter.transform, false);
        Stretch(points.rectTransform, new Vector2(80f, 25f), new Vector2(-80f, -25f));
        points.alignment = TextAlignmentOptions.Center;
        points.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        points.fontSize = 46f;
        points.fontStyle = FontStyles.Bold;

        Image energy = ImageAt(scene, "Canvas/SafeArea/CollectiveProgress");
        energy.sprite = S("Assets/Art/UI/Home/energy-status-container.png");
        energy.preserveAspect = true;
        Center(energy.rectTransform, new Vector2(600f, 30f), new Vector2(0f, -25f));
        Disable(scene, "Canvas/SafeArea/CollectiveProgress/ProgressLabel");

        Transform card = Find(scene, "Canvas/SafeArea/ActivityCard");
        Image cardImage = card.GetComponent<Image>();
        cardImage.sprite = S("Assets/Art/UI/Home/action-card-1.png");
        cardImage.preserveAspect = true;
        Center(card as RectTransform, new Vector2(500f, 350f), new Vector2(-262f, -515f));
        Disable(scene, "Canvas/SafeArea/ActivityCard/ActivityTitle");
        Disable(scene, "Canvas/SafeArea/ActivityCard/ActivityDescription");
        Button play = ComponentAt<Button>(scene, "Canvas/SafeArea/ActivityCard/PlayButton");
        Stretch(play.transform as RectTransform);
        MakeButtonTransparent(play);
        DisableDirectText(play.transform);

        Image secondCard = CreateImage(safe, "ComingSoonCard", S("Assets/Art/UI/Home/action-card-2.png"));
        Center(secondCard.rectTransform, new Vector2(500f, 350f), new Vector2(262f, -515f));

        Save(scene);
    }

    // ---------------------------------------------------------------------
    // Customização e avatar do perfil
    // ---------------------------------------------------------------------

    private static void InstallCustomization()
    {
        Scene scene = Open("Assets/Scenes/Customization/Customization.unity");
        Transform canvas = Find(scene, "Canvas");
        Transform safe = Find(scene, "Canvas/SafeArea");
        ConfigureCanvas(canvas);
        ConfigureSafeArea(safe);
        SetFullScreen(ImageAt(scene, "Canvas/Background"), S(DarkBackgroundPath), false);

        Button back = ComponentAt<Button>(scene, "Canvas/SafeArea/Header/BackButton");
        SetButtonSprite(back, S("Assets/Art/UI/PersonalizarPersonagem1/back-btn.png"));
        AnchorTop(back.transform as RectTransform, new Vector2(110f, 110f), new Vector2(85f, -90f), true);
        DisableDirectText(back.transform);
        Disable(scene, "Canvas/SafeArea/Header/Title");

        Transform preview = Find(scene, "Canvas/SafeArea/AvatarPreview");
        AnchorTop(preview as RectTransform, new Vector2(1080f, 690f), new Vector2(0f, -410f), true);
        AvatarView avatarView = Find(scene, "Canvas/SafeArea/AvatarPreview/AvatarRoot").GetComponent<AvatarView>();
        BuildArtAvatar(avatarView, true);

        Image points = CreateImage(safe, "CustomizationPoints", S("Assets/Art/UI/PersonalizarPersonagem1/points.png"));
        AnchorTop(points.rectTransform, new Vector2(250f, 84f), new Vector2(-160f, -95f), false);
        TMP_Text pointsValue = CreateText(points.transform, "Value", "0", 34f, Color.white);
        Stretch(pointsValue.rectTransform, new Vector2(75f, 8f), new Vector2(-20f, -8f));
        pointsValue.alignment = TextAlignmentOptions.Center;
        BreakPointsTextBinder pointsBinder = GetOrAdd<BreakPointsTextBinder>(points.gameObject);
        SetObject(pointsBinder, "target", pointsValue);

        Transform tabs = Find(scene, "Canvas/SafeArea/TabsBar");
        AnchorBottom(tabs as RectTransform, new Vector2(960f, 120f), new Vector2(0f, 790f), true);
        HorizontalLayoutGroup tabLayout = GetOrAdd<HorizontalLayoutGroup>(tabs.gameObject);
        tabLayout.spacing = 22f;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = false;
        tabLayout.childControlHeight = false;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = false;

        Button hat = ComponentAt<Button>(scene, "Canvas/SafeArea/TabsBar/HatTab");
        Button face = ComponentAt<Button>(scene, "Canvas/SafeArea/TabsBar/FaceTab");
        Button color = ComponentAt<Button>(scene, "Canvas/SafeArea/TabsBar/ColorTab");
        SetButtonSprite(hat, S("Assets/Art/UI/PersonalizarPersonagem1/tab-bone.png"));
        SetButtonSprite(face, S("Assets/Art/UI/PersonalizarPersonagem1/tab-cara.png"));
        SetButtonSprite(color, S("Assets/Art/UI/PersonalizarPersonagem1/tab-cor.png"));
        Center(hat.transform as RectTransform, new Vector2(290f, 100f), Vector2.zero);
        Center(face.transform as RectTransform, new Vector2(290f, 100f), Vector2.zero);
        Center(color.transform as RectTransform, new Vector2(290f, 100f), Vector2.zero);
        DisableDirectText(hat.transform);
        DisableDirectText(face.transform);
        DisableDirectText(color.transform);

        CustomizationTabArtController tabArt = GetOrAdd<CustomizationTabArtController>(tabs.gameObject);
        SetObject(tabArt, "hatButton", hat);
        SetObject(tabArt, "faceButton", face);
        SetObject(tabArt, "colorButton", color);
        SetObject(tabArt, "hatActive", S("Assets/Art/UI/PersonalizarPersonagem1/tab-bone.png"));
        SetObject(tabArt, "hatInactive", S("Assets/Art/UI/PersonalizarPersonagem1/tab-bone.png"));
        SetObject(tabArt, "faceActive", S("Assets/Art/UI/PersonalizarPersonagem3/tab-bone (2).png"));
        SetObject(tabArt, "faceInactive", S("Assets/Art/UI/PersonalizarPersonagem1/tab-cara.png"));
        SetObject(tabArt, "colorActive", S("Assets/Art/UI/PersonalizarPersonagem2/tab-bone (1).png"));
        SetObject(tabArt, "colorInactive", S("Assets/Art/UI/PersonalizarPersonagem1/tab-cor.png"));

        Transform scroll = Find(scene, "Canvas/SafeArea/OptionsScrollView");
        AnchorBottom(scroll as RectTransform, new Vector2(980f, 700f), new Vector2(0f, 370f), true);
        Transform content = Find(scene, "Canvas/SafeArea/OptionsScrollView/Viewport/Content");
        RectTransform contentRect = content as RectTransform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        tabs.SetSiblingIndex(scroll.GetSiblingIndex() + 1);
        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(210f, 192f);
        grid.spacing = new Vector2(28f, 26f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        Sprite selectedCard = S("Assets/Art/UI/PersonalizarPersonagem1/card-em-uso (1).png");
        Sprite costCard = S("Assets/Art/UI/PersonalizarPersonagem1/card-300-red.png");
        Sprite level6 = S("Assets/Art/UI/PersonalizarPersonagem1/card-nivel6.png");
        Sprite level8 = S("Assets/Art/UI/PersonalizarPersonagem1/card-nivel8.png");
        Sprite level12 = S("Assets/Art/UI/PersonalizarPersonagem1/card-nivel8 (1).png");

        Sprite[] hatSprites =
        {
            S("Assets/Art/UI/PersonalizarPersonagem1/hat-icon.png"),
            S("Assets/Art/UI/PersonalizarPersonagem1/hat-icon-green.png"),
            S("Assets/Art/UI/PersonalizarPersonagem1/hat-top-red.png")
        };
        Sprite[] eyes = FaceEyes();
        Sprite[] colors = FaceColors();

        GameObject[] hairOptions = FindOptions(content, "Hair", hatSprites.Length);
        for (int i = 0; i < hairOptions.Length; i++)
            ConfigureOption(hairOptions[i], hatSprites[i], i == 0 ? selectedCard : null, null);

        GameObject[] faceOptions = FindOptions(content, "Outfit", eyes.Length);
        for (int i = 0; i < faceOptions.Length; i++)
        {
            Sprite cardArt = i == 0 ? selectedCard : i == 3 ? costCard : i == 4 ? level6 : i == 5 ? level8 : i == 6 ? level12 : null;
            ConfigureOption(faceOptions[i], eyes[i], cardArt, FaceMouths()[i]);
        }

        GameObject[] colorOptions = FindOptions(content, "Accessory", colors.Length);
        for (int i = 0; i < colorOptions.Length; i++)
        {
            Sprite cardArt = i == 0 ? selectedCard : i == 3 ? costCard : i == 4 ? level6 : i == 5 ? level8 : null;
            ConfigureOption(colorOptions[i], colors[i], cardArt, null);
        }

        CustomizationController controller = Find(scene, "CustomizationController").GetComponent<CustomizationController>();
        SetObjectArray(controller, "hairOptions", hairOptions);
        SetObjectArray(controller, "outfitOptions", faceOptions);
        SetObjectArray(controller, "accessoryOptions", colorOptions);
        SetObject(controller, "hairButton", hat);
        SetObject(controller, "outfitButton", face);
        SetObject(controller, "accessoryButton", color);

        Transform confirm = Find(scene, "Canvas/SafeArea/Header/ConfirmButton");
        AnchorBottom(confirm as RectTransform, new Vector2(350f, 105f), new Vector2(0f, 65f), true);

        Save(scene);
    }

    private static void BuildArtAvatar(AvatarView avatarView, bool large)
    {
        Transform root = avatarView.transform;
        SetChildrenActive(root, new[] { "Body", "HeadAnchor", "TorsoAnchor" }, false);

        GameObject artRoot = GetOrCreateUI(root, "ArtAvatarRoot");
        Center(artRoot.transform as RectTransform, large ? new Vector2(560f, 560f) : new Vector2(500f, 500f), Vector2.zero);

        Image baseImage = CreateImage(artRoot.transform, "Base", S("Assets/Art/UI/PersonalizarPersonagem1/avatar-circle.png"));
        Center(baseImage.rectTransform, new Vector2(500f, 500f), Vector2.zero);

        Image faceColor = CreateImage(artRoot.transform, "FaceColor", FaceColors()[0]);
        Center(faceColor.rectTransform, new Vector2(250f, 236f), new Vector2(0f, -35f));

        Image hat = CreateImage(artRoot.transform, "Hat", S("Assets/Art/UI/PersonalizarPersonagem1/hat-icon.png"));
        Center(hat.rectTransform, new Vector2(310f, 150f), new Vector2(0f, 120f));

        Image eyes = CreateImage(artRoot.transform, "Eyes", FaceEyes()[0]);
        Center(eyes.rectTransform, new Vector2(120f, 80f), new Vector2(0f, -5f));

        Image mouth = CreateImage(artRoot.transform, "Mouth", FaceMouths()[0]);
        Center(mouth.rectTransform, new Vector2(125f, 72f), new Vector2(0f, -72f));

        Image browLeft = CreateImage(artRoot.transform, "BrowLeft", S("Assets/Art/UI/PersonalizarPersonagem3/Rectangle 7.png"));
        Center(browLeft.rectTransform, new Vector2(70f, 58f), new Vector2(-62f, 45f));
        Image browRight = CreateImage(artRoot.transform, "BrowRight", S("Assets/Art/UI/PersonalizarPersonagem3/Rectangle 8.png"));
        Center(browRight.rectTransform, new Vector2(70f, 58f), new Vector2(62f, 45f));
        browRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        SetObject(avatarView, "artAvatarBaseImage", baseImage);
        SetObject(avatarView, "artFaceColorImage", faceColor);
        SetObject(avatarView, "artHatImage", hat);
        SetObject(avatarView, "artEyesImage", eyes);
        SetObject(avatarView, "artMouthImage", mouth);
        SetObject(avatarView, "artBrowLeftImage", browLeft);
        SetObject(avatarView, "artBrowRightImage", browRight);
        SetSpriteArray(avatarView, "artFaceColorOptions", FaceColors());
        SetSpriteArray(avatarView, "artHatOptions", new[]
        {
            S("Assets/Art/UI/PersonalizarPersonagem1/hat-icon.png"),
            S("Assets/Art/UI/PersonalizarPersonagem1/hat-icon-green.png"),
            S("Assets/Art/UI/PersonalizarPersonagem1/hat-top-red.png")
        });
        SetSpriteArray(avatarView, "artEyesOptions", FaceEyes());
        SetSpriteArray(avatarView, "artMouthOptions", FaceMouths());
        SetObject(avatarView, "artBrowLeftSprite", S("Assets/Art/UI/PersonalizarPersonagem3/Rectangle 7.png"));
        SetObject(avatarView, "artBrowRightSprite", S("Assets/Art/UI/PersonalizarPersonagem3/Rectangle 8.png"));
        EditorUtility.SetDirty(avatarView);
    }

    private static Sprite[] FaceColors() => new[]
    {
        S("Assets/Art/UI/PersonalizarPersonagem2/face.png"),
        S("Assets/Art/UI/PersonalizarPersonagem2/face (1).png"),
        S("Assets/Art/UI/PersonalizarPersonagem2/face (2).png"),
        S("Assets/Art/UI/PersonalizarPersonagem2/face (3).png"),
        S("Assets/Art/UI/PersonalizarPersonagem2/face (4).png"),
        S("Assets/Art/UI/PersonalizarPersonagem2/face (5).png")
    };

    private static Sprite[] FaceEyes() => new[]
    {
        S("Assets/Art/UI/PersonalizarPersonagem3/eyes.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/eyes 1.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/eyes 2.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/eyes-1.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/eye-left.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/eye-right.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/eyes.png")
    };

    private static Sprite[] FaceMouths() => new[]
    {
        S("Assets/Art/UI/PersonalizarPersonagem3/beak.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/beak 1.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/beak 2.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/beak-1.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/beak 3.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/beak 4.png"),
        S("Assets/Art/UI/PersonalizarPersonagem3/Ellipse 2.png")
    };

    private static void ConfigureOption(GameObject option, Sprite thumbnailSprite, Sprite cardSprite, Sprite secondarySprite)
    {
        if (option == null)
            return;

        Image rootImage = GetOrAdd<Image>(option);
        rootImage.sprite = cardSprite;
        rootImage.preserveAspect = cardSprite != null;
        rootImage.color = cardSprite != null ? Color.white : new Color(0.17f, 0.17f, 0.17f, 1f);
        rootImage.raycastTarget = true;

        Image thumbnail = CreateImage(option.transform, "ArtThumbnail", thumbnailSprite);
        Center(thumbnail.rectTransform, new Vector2(125f, 105f), secondarySprite == null ? Vector2.zero : new Vector2(0f, 24f));
        thumbnail.raycastTarget = false;

        if (secondarySprite != null)
        {
            Image secondary = CreateImage(option.transform, "ArtSecondary", secondarySprite);
            Center(secondary.rectTransform, new Vector2(100f, 58f), new Vector2(0f, -48f));
            secondary.raycastTarget = false;
        }

        DisableDirectText(option.transform);
    }

    private static GameObject[] FindOptions(Transform content, string prefix, int count)
    {
        var options = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            Transform found = content.Find(prefix + (i + 1));
            if (found == null)
                throw new InvalidOperationException($"Customization option {prefix}{i + 1} was not found.");

            found.gameObject.SetActive(true);
            options[i] = found.gameObject;
        }

        for (int i = count + 1; i <= 12; i++)
        {
            Transform extra = content.Find(prefix + i);
            if (extra != null)
                extra.gameObject.SetActive(false);
        }

        return options;
    }

    // ---------------------------------------------------------------------
    // Energy Station
    // ---------------------------------------------------------------------

    private static void InstallEnergyStation()
    {
        Scene scene = Open("Assets/Scenes/EnergyStation/EnergyStation.unity");
        Transform canvas = Find(scene, "Canvas");
        Transform safe = Find(scene, "Canvas/SafeArea");
        ConfigureCanvas(canvas);
        ConfigureSafeArea(safe);
        SetFullScreen(ImageAt(scene, "Canvas/Background"), S(DarkBackgroundPath), false);

        Transform header = Find(scene, "Canvas/SafeArea/Header");
        Disable(scene, "Canvas/SafeArea/Header/EnergyStationTitle");
        Image headerArt = CreateImage(header, "ArtHeader", S("Assets/Art/UI/EnergyBreak/header.png"));
        AnchorTop(headerArt.rectTransform, new Vector2(520f, 70f), new Vector2(0f, -95f), true);
        TMP_Text timer = ComponentAt<TMP_Text>(scene, "Canvas/SafeArea/Header/TimerLabel");
        AnchorTop(timer.rectTransform, new Vector2(280f, 100f), new Vector2(0f, -205f), true);
        timer.fontSize = 64f;
        timer.fontStyle = FontStyles.Bold;

        Image avatar = ImageAt(scene, "Canvas/SafeArea/AvatarArea/Avatar");
        avatar.sprite = S("Assets/Art/UI/EnergyBreak/monster-character.png");
        avatar.preserveAspect = true;
        Center(avatar.rectTransform, new Vector2(520f, 520f), new Vector2(0f, 215f));
        MequiEnergyArtFeedback feedback = GetOrAdd<MequiEnergyArtFeedback>(avatar.gameObject);
        SetObject(feedback, "targetImage", avatar);
        SetObject(feedback, "normalSprite", S("Assets/Art/UI/EnergyBreak/monster-character.png"));
        SetObject(feedback, "successSprite", S("Assets/Art/UI/EnergyBreak/monster-character (1).png"));

        Image dropZone = ImageAt(scene, "Canvas/SafeArea/AvatarArea/Avatar/DropZone");
        dropZone.sprite = S("Assets/Art/UI/EnergyBreak/dashed-drop-zone.png");
        dropZone.preserveAspect = true;
        dropZone.color = Color.white;
        dropZone.raycastTarget = true;
        Center(dropZone.rectTransform, new Vector2(620f, 620f), Vector2.zero);

        Transform interactionArea = Find(scene, "Canvas/SafeArea/InteractionArea");
        AnchorBottom(interactionArea as RectTransform, new Vector2(970f, 340f), new Vector2(0f, 310f), true);
        Image tray = CreateImage(interactionArea, "ArtItemsTray", S("Assets/Art/UI/EnergyBreak/items-tray.png"));
        Center(tray.rectTransform, new Vector2(930f, 222f), Vector2.zero);
        tray.transform.SetAsFirstSibling();

        Transform interactions = Find(scene, "Canvas/SafeArea/InteractionArea/InteractionObjects");
        Center(interactions as RectTransform, new Vector2(900f, 235f), Vector2.zero);
        Transform oldWater = interactions.Find("WaterBottle");
        if (oldWater != null)
            UnityEngine.Object.DestroyImmediate(oldWater.gameObject);

        CreateEnergyItem(interactions, "Hydration", -324f,
            S("Assets/Art/UI/EnergyBreak/Vector.png"),
            S("Assets/Art/UI/EnergyBreak/Group 4.png"),
            S("Assets/Art/UI/EnergyBreak/Group 1.png"),
            S("Assets/Art/UI/EnergyBreak/Hidratação.png"),
            "hydration", "Hidratação concluída!",
            S("Assets/Art/UI/EnergyBreak/Hidrata#U00e7#U00e3o.png"));

        CreateEnergyItem(interactions, "Stretch", -108f,
            S("Assets/Art/UI/EnergyBreak/Vector 1.png"),
            S("Assets/Art/UI/EnergyBreak/Group 2.png"), null,
            S("Assets/Art/UI/EnergyBreak/Alongar.png"),
            "stretch", "Alongamento concluído!", null);

        CreateEnergyItem(interactions, "Bathroom", 108f,
            S("Assets/Art/UI/EnergyBreak/Vector 2.png"),
            S("Assets/Art/UI/EnergyBreak/03-banheiro 1.png"), null,
            S("Assets/Art/UI/EnergyBreak/Banheiro.png"),
            "bathroom", "Pausa para o banheiro registrada!", null);

        CreateEnergyItem(interactions, "Break", 324f,
            S("Assets/Art/UI/EnergyBreak/Vector 2.png"),
            S("Assets/Art/UI/EnergyBreak/Group 3.png"),
            S("Assets/Art/UI/EnergyBreak/Group.png"),
            S("Assets/Art/UI/EnergyBreak/Intervalo.png"),
            "break", "Intervalo concluído!", null);

        Transform progress = Find(scene, "Canvas/SafeArea/ProgressBar");
        AnchorBottom(progress as RectTransform, new Vector2(680f, 84f), new Vector2(0f, 560f), true);
        Transform complete = Find(scene, "Canvas/SafeArea/CompleteButton");
        AnchorBottom(complete as RectTransform, new Vector2(390f, 108f), new Vector2(0f, 85f), true);

        Save(scene);
    }

    private static void CreateEnergyItem(
        Transform parent,
        string name,
        float x,
        Sprite cardSprite,
        Sprite normalIcon,
        Sprite highlightedIcon,
        Sprite labelSprite,
        string interactionId,
        string feedbackMessage,
        Sprite compatibilitySprite)
    {
        GameObject slot = GetOrCreateUI(parent, name + "Slot");
        Image slotImage = GetOrAdd<Image>(slot);
        slotImage.sprite = S("Assets/Art/UI/EnergyBreak/item-placeholder.png");
        slotImage.preserveAspect = true;
        slotImage.raycastTarget = false;
        Center(slot.transform as RectTransform, new Vector2(205f, 205f), new Vector2(x, 0f));

        GameObject draggableObject = GetOrCreateUI(slot.transform, name);
        Image card = GetOrAdd<Image>(draggableObject);
        card.sprite = cardSprite;
        card.preserveAspect = true;
        card.raycastTarget = true;
        Center(draggableObject.transform as RectTransform, new Vector2(178f, 170f), new Vector2(0f, 10f));
        GetOrAdd<CanvasGroup>(draggableObject);

        Image icon = CreateImage(draggableObject.transform, "Icon", normalIcon);
        Center(icon.rectTransform, new Vector2(102f, 102f), new Vector2(0f, 21f));
        icon.raycastTarget = false;
        Image label = CreateImage(draggableObject.transform, "Label", labelSprite);
        Center(label.rectTransform, new Vector2(135f, 35f), new Vector2(0f, -55f));
        label.raycastTarget = false;

        DraggableInteraction interaction = GetOrAdd<DraggableInteraction>(draggableObject);
        SetString(interaction, "interactionId", interactionId);
        SetString(interaction, "reactionTrigger", "Happy");
        SetString(interaction, "feedbackMessage", feedbackMessage);

        MequiDraggableArtState artState = GetOrAdd<MequiDraggableArtState>(draggableObject);
        SetObject(artState, "iconImage", icon);
        SetObject(artState, "normalSprite", normalIcon);
        SetObject(artState, "highlightedSprite", highlightedIcon);
        SetObject(artState, "legacyCompatibilitySprite", compatibilitySprite);
    }

    private static void ConfigureCatalog()
    {
        AvatarCustomizationCatalog catalog = AssetDatabase.LoadAssetAtPath<AvatarCustomizationCatalog>(
            "Assets/ScriptableObjects/AvatarCustomizationCatalog.asset");
        if (catalog == null)
            return;

        SerializedObject serialized = new SerializedObject(catalog);
        ConfigureCategory(serialized.FindProperty("hairItems"), 3, "hat", 0);
        ConfigureCategory(serialized.FindProperty("outfitItems"), 7, "face", 1);
        ConfigureCategory(serialized.FindProperty("accessoryItems"), 6, "color", 2);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void ConfigureCategory(SerializedProperty list, int count, string prefix, int category)
    {
        list.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("Id").stringValue = $"{prefix}_{i:00}";
            item.FindPropertyRelative("Category").enumValueIndex = category;
            item.FindPropertyRelative("OptionIndex").intValue = i;
            item.FindPropertyRelative("DisplayName").stringValue = $"{prefix} {i + 1}";

            int unlockType = 0;
            int cost = 0;
            int level = 1;
            if (i == 3)
            {
                unlockType = 1;
                cost = 300;
            }
            else if (i >= 4)
            {
                unlockType = 2;
                level = i == 4 ? 6 : i == 5 ? 8 : 12;
            }

            item.FindPropertyRelative("UnlockType").enumValueIndex = unlockType;
            item.FindPropertyRelative("BreakPointCost").intValue = cost;
            item.FindPropertyRelative("RequiredLevel").intValue = level;
        }
    }

    // ---------------------------------------------------------------------
    // Funções auxiliares do Editor
    // ---------------------------------------------------------------------

    private static Scene Open(string path) => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Sprite S(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    private static Transform Find(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0])
                continue;

            Transform current = root.transform;
            for (int i = 1; i < parts.Length && current != null; i++)
                current = current.Find(parts[i]);
            return current;
        }
        return null;
    }

    private static T ComponentAt<T>(Scene scene, string path) where T : Component
    {
        Transform found = Find(scene, path);
        if (found == null)
            throw new InvalidOperationException($"Required object was not found: {path}");
        T component = found.GetComponent<T>();
        if (component == null)
            throw new InvalidOperationException($"Required component {typeof(T).Name} was not found at {path}");
        return component;
    }

    private static Image ImageAt(Scene scene, string path) => ComponentAt<Image>(scene, path);

    private static void ConfigureCanvas(Transform canvasTransform)
    {
        if (canvasTransform == null)
            return;
        CanvasScaler scaler = canvasTransform.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }

    private static void ConfigureSafeArea(Transform safe)
    {
        if (safe == null)
            return;

        foreach (MonoBehaviour component in safe.GetComponents<MonoBehaviour>())
        {
            if (component != null && component.GetType().FullName == "Crystal.SafeArea")
                UnityEngine.Object.DestroyImmediate(component);
        }
        GetOrAdd<SafeAreaFitter>(safe.gameObject);
        Stretch(safe as RectTransform);
    }

    private static void SetFullScreen(Image image, Sprite sprite, bool preserveAspect)
    {
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        image.transform.SetAsFirstSibling();
    }

    private static GameObject GetOrCreateUI(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;
        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite)
    {
        GameObject gameObject = GetOrCreateUI(parent, name);
        Image image = GetOrAdd<Image>(gameObject);
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float fontSize, Color color)
    {
        GameObject gameObject = GetOrCreateUI(parent, name);
        TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(gameObject);
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetButtonSprite(Button button, Sprite sprite)
    {
        Image image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;
        button.targetGraphic = image;
    }

    private static void MakeButtonTransparent(Button button)
    {
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;
            button.targetGraphic = image;
        }
    }

    private static void EnsurePersistentListener(Button button, LoginController controller, string method)
    {
        // Evita duplicar o listener já existente durante uma reinstalação manual.
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == controller
                && button.onClick.GetPersistentMethodName(i) == method)
                return;
        }
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, controller.Login);
    }

    private static void Disable(Scene scene, string path)
    {
        Transform found = Find(scene, path);
        if (found != null)
            found.gameObject.SetActive(false);
    }

    private static void DisableDirectText(Transform parent)
    {
        foreach (TMP_Text text in parent.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.parent == parent)
                text.gameObject.SetActive(false);
        }
    }

    private static void SetChildrenActive(Transform parent, IEnumerable<string> names, bool active)
    {
        foreach (string name in names)
        {
            Transform child = parent.Find(name);
            if (child != null)
                child.gameObject.SetActive(active);
        }
    }

    private static void Stretch(RectTransform rect)
        => Stretch(rect, Vector2.zero, Vector2.zero);

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void AnchorTop(RectTransform rect, Vector2 size, Vector2 position, bool fromLeft)
    {
        Vector2 anchor = new Vector2(fromLeft ? 0f : 1f, 1f);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void AnchorBottom(RectTransform rect, Vector2 size, Vector2 position, bool centered)
    {
        Vector2 anchor = new Vector2(centered ? 0.5f : 0f, 0f);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Serialized property {propertyName} was not found on {target.name}.");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetString(UnityEngine.Object target, string propertyName, string value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"Serialized property {propertyName} was not found on {target.name}.");
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetObjectArray(Component target, string propertyName, GameObject[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetSpriteArray(Component target, string propertyName, Sprite[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
#endif
