using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Ferramenta de edição: grava os objetos na cena para que possam ser ajustados no Inspector.
internal static class RushBalanceScenePolish
{
    private const string ScenePath = "Assets/Scenes/RushBalance/RushBalance2.unity";

    [MenuItem("Tools/Rush Balance/Aplicar acabamento visual _F7")]
    private static void Apply()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var objects = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects()) Collect(root, objects);
            GameObject Find(string name) => objects.Find(go => go.name == name);

            Sprite track = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/ComboCrew/Polish/order_progress_track.png");
            Sprite fill = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/ComboCrew/Polish/order_progress_fill.png");
            Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/ComboCrew/Polish/worker_profile_panel.png");
            Sprite slot = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/ComboCrew/Polish/worker_card_clean.png");
            Sprite bubbleRight = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/ComboCrew/Polish/break_bubble_right.png");
            Sprite bubbleLeft = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/ComboCrew/Polish/break_bubble_left.png");
            if (track == null || fill == null || panel == null || slot == null
                || bubbleRight == null || bubbleLeft == null)
                throw new InvalidOperationException("Arte de interface do Combo Crew ausente.");

            Find("Subtitulo").SetActive(false);
            Find("Entregues").SetActive(false);

            Image timerPanel = Find("TempoPill").GetComponent<Image>();
            timerPanel.sprite = track;
            timerPanel.type = Image.Type.Sliced;
            timerPanel.color = Color.white;
            RectTransform timerRect = timerPanel.rectTransform;
            timerRect.sizeDelta = new Vector2(190f, 66f);
            TMP_Text timerText = Find("Tempo").GetComponent<TMP_Text>();
            timerText.fontStyle = FontStyles.Bold;
            timerText.fontSize = 43f;
            timerText.color = new Color(1f, .78f, .17f);

            GameObject back = Find("Voltar");
            Image backImage = back.GetComponent<Image>();
            backImage.sprite = track;
            backImage.type = Image.Type.Sliced;
            backImage.color = Color.white;
            TMP_Text backArrow = back.GetComponentInChildren<TMP_Text>(true);
            backArrow.color = new Color(1f, .78f, .17f);
            backArrow.fontStyle = FontStyles.Bold;

            GameObject slots = Find("SlotsPedidos");
            Transform holder = slots.transform.Find("MolduraDaFila");
            if (holder == null)
            {
                GameObject holderObject = new GameObject("MolduraDaFila", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                holder = holderObject.transform;
                holder.SetParent(slots.transform, false);
                holder.SetAsFirstSibling();
            }
            RectTransform holderRect = (RectTransform)holder;
            holderRect.anchorMin = holderRect.anchorMax = new Vector2(.5f, .5f);
            holderRect.anchoredPosition = Vector2.zero;
            holderRect.sizeDelta = new Vector2(980f, 360f);
            Image holderImage = holder.GetComponent<Image>();
            holderImage.sprite = panel;
            holderImage.type = Image.Type.Sliced;
            holderImage.color = new Color(1f, 1f, 1f, .9f);
            holderImage.raycastTarget = false;

            for (int i = 1; i <= 4; i++)
            {
                Transform slotTransform = slots.transform.Find("Slot_" + i);
                Image slotImage = slotTransform.GetComponent<Image>();
                slotImage.sprite = slot;
                slotImage.type = Image.Type.Simple;
                slotImage.preserveAspect = false;
                slotImage.color = new Color(1f, 1f, 1f, .8f);
                // O Image do slot continua recebendo o drop.
            }

            var cards = FindAll<RushOrderView>(scene);
            foreach (RushOrderView card in cards)
            {
                SerializedObject serialized = new SerializedObject(card);
                Image progress = (Image)serialized.FindProperty("progressFill").objectReferenceValue;
                if (progress == null) throw new InvalidOperationException("Card sem ProgressoPedido.");
                RectTransform barRoot = progress.rectTransform.parent as RectTransform;
                Image barTrack = null;
                foreach (Transform child in barRoot)
                    if (child.name == "TrilhoPreparo") barTrack = child.GetComponent<Image>();
                if (barRoot.name == "TrilhoPreparo") barTrack = barRoot.GetComponent<Image>();
                if (barTrack == null) throw new InvalidOperationException("Card sem TrilhoPreparo.");
                barTrack.sprite = track;
                barTrack.type = Image.Type.Sliced;
                barTrack.color = Color.white;
                barTrack.rectTransform.sizeDelta = new Vector2(190f, 20f);
                progress.sprite = fill;
                progress.type = Image.Type.Filled;
                progress.fillMethod = Image.FillMethod.Horizontal;
                progress.fillOrigin = (int)Image.OriginHorizontal.Left;
                progress.fillAmount = .58f; // prévia editável na cena; a partida inicia vazia.
                progress.preserveAspect = false;
                progress.color = new Color(1f, .78f, .17f);
                RectTransform progressRect = progress.rectTransform;
                progressRect.anchorMin = progressRect.anchorMax = new Vector2(.5f, .5f);
                progressRect.pivot = new Vector2(.5f, .5f);
                progressRect.anchoredPosition = new Vector2(0f, -78f);
                progressRect.sizeDelta = new Vector2(176f, 8f);
                progressRect.localScale = Vector3.one;
                EnlargeFoodIcons(serialized.FindProperty("itemIcons"), 49f);
            }

            var customers = FindAll<RushCustomerView>(scene);
            for (int i = 0; i < customers.Count; i++)
            {
                SerializedObject serialized = new SerializedObject(customers[i]);
                GameObject balloon = (GameObject)serialized.FindProperty("balloon").objectReferenceValue;
                Image bubbleImage = balloon.GetComponent<Image>();
                bool appearsRight = i < 2;
                bubbleImage.sprite = appearsRight ? bubbleRight : bubbleLeft;
                bubbleImage.type = Image.Type.Simple;
                bubbleImage.color = Color.white;
                RectTransform bubbleRect = (RectTransform)balloon.transform;
                bubbleRect.anchoredPosition = new Vector2(appearsRight ? 48f : -48f, 200f);
                bubbleRect.sizeDelta = new Vector2(200f, 124f);
                EnlargeFoodIcons(serialized.FindProperty("itemIcons"), 47f);

                GameObject patienceTrack = (GameObject)serialized.FindProperty("patienceTrack").objectReferenceValue;
                Image patienceTrackImage = patienceTrack.GetComponent<Image>();
                patienceTrackImage.sprite = track;
                patienceTrackImage.type = Image.Type.Sliced;
                patienceTrackImage.color = Color.white;
                patienceTrackImage.rectTransform.sizeDelta = new Vector2(138f, 14f);
                Image patienceFill = (Image)serialized.FindProperty("patienceFill").objectReferenceValue;
                patienceFill.sprite = fill;
                patienceFill.type = Image.Type.Filled;
                patienceFill.fillMethod = Image.FillMethod.Horizontal;
                patienceFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                patienceFill.fillAmount = .7f;
                patienceFill.rectTransform.anchoredPosition = patienceTrackImage.rectTransform.anchoredPosition;
                patienceFill.rectTransform.sizeDelta = new Vector2(126f, 6f);
                patienceFill.rectTransform.pivot = new Vector2(.5f, .5f);
                patienceFill.rectTransform.localScale = Vector3.one;
                TMP_Text speech = (TMP_Text)serialized.FindProperty("speech").objectReferenceValue;
                speech.fontStyle = FontStyles.Bold;
                speech.color = Color.white;
                speech.fontSize = 17f;
            }

            CorrectLayersAndBars(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RushBalance] Acabamento visual aplicado e salvo na cena.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }


    // Aplica à cena carregada para que um salvamento do Editor não restaure a versão antiga.
    [InitializeOnLoadMethod]
    private static void QueueVisualRepair()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || Application.isBatchMode
            || SessionState.GetBool("RushBalance.VisualRepair.20261021.v5", false)) return;
        EditorApplication.update -= RepairWhenReady;
        EditorApplication.update += RepairWhenReady;
    }

    private static void RepairWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded) return;
        EditorApplication.update -= RepairWhenReady;
        // Cenas já corrigidas continuam livres para ajustes manuais no Inspector.
        if (FindAll<Image>(scene).FindAll(image => image.name == "ContornoPreto").Count >= 9
            && FindAll<RushCustomerView>(scene).TrueForAll(view =>
                new SerializedObject(view).FindProperty("circularPatience").boolValue
                && new SerializedObject(view).FindProperty("balloonGroup").objectReferenceValue != null
                && ((GameObject)new SerializedObject(view).FindProperty("balloon").objectReferenceValue)
                    .GetComponent<Image>().sprite.name == "rush_speech_bubble")
            && FindAll<RushOrderView>(scene).TrueForAll(view =>
                new SerializedObject(view).FindProperty("progressTrack").objectReferenceValue != null))
        {
            SessionState.SetBool("RushBalance.VisualRepair.20261021.v5", true);
            return;
        }
        try
        {
            CorrectLayersAndBars(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Não foi possível salvar RushBalance2.");
            SessionState.SetBool("RushBalance.VisualRepair.20261021.v5", true);
            System.IO.File.WriteAllText("Temp/RushBalanceVisualRepair.applied", DateTime.UtcNow.ToString("O"));
            Debug.Log("[RushBalance] Rostos, bancada e nove contornos corrigidos na cena aberta e salvos.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    [MenuItem("Tools/Rush Balance/Atualizar baloes e paciencia")]
    [MenuItem("Tools/Rush Balance/Corrigir rostos, bancada e barras")]
    private static void RepairVisuals()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("Abra RushBalance2 fora do Play Mode para aplicar a correção.");
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        CorrectLayersAndBars(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void CorrectLayersAndBars(Scene scene)
    {
        Sprite trackSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ComboCrew/Polish/order_progress_track.png");
        Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ComboCrew/Polish/order_progress_fill.png");
        Sprite outlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/ComboCrew/order_progress_outline.png");
        if (trackSprite == null || fillSprite == null || outlineSprite == null)
            throw new InvalidOperationException("Sprites das barras do Combo Crew ausentes.");
        foreach (RushCustomerView customer in FindAll<RushCustomerView>(scene))
        {
            SerializedObject data = new SerializedObject(customer);
            Transform character = (Transform)data.FindProperty("character").objectReferenceValue;
            string[] layers = { "CorpoCompleto", "Corpo", "RostoBase", "CoberturaRosto",
                "RostoFeliz", "RostoSerio", "RostoFrustrado", "Chapeu", "BalaoPedido" };
            foreach (string layer in layers)
            {
                Transform child = character.Find(layer);
                if (child != null) child.SetAsLastSibling();
            }
            ConfigureCustomerBubbleAndPatience(data);
        }
        foreach (RushOrderView card in FindAll<RushOrderView>(scene))
        {
            SerializedObject cardData = new SerializedObject(card);
            Image fill = (Image)cardData.FindProperty("progressFill").objectReferenceValue;
            Transform parent = fill.transform.parent;
            RectTransform track = (parent.name == "TrilhoPreparo" ? parent : parent.Find("TrilhoPreparo")) as RectTransform;
            if (track == null) throw new InvalidOperationException("Card sem TrilhoPreparo.");
            ConfigureBar(fill, track, 20f, trackSprite, fillSprite, outlineSprite);
            cardData.FindProperty("progressTrack").objectReferenceValue = track;
            cardData.ApplyModifiedPropertiesWithoutUndo();
        }
        var objects = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects()) Collect(root, objects);
        GameObject counter = objects.Find(go => go.name == "bancada");
        GameObject area = objects.Find(go => go.name == "AreaClientes");
        if (counter != null && area != null)
        {
            RectTransform rect = (RectTransform)counter.transform;
            rect.SetParent(area.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(-15f, -300f);
            rect.SetAsLastSibling();
            counter.GetComponent<Image>().raycastTarget = false;
        }
    }

    private static void ConfigureCustomerBubbleAndPatience(SerializedObject data)
    {
        Sprite disk = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/AvatarCustomization/body_base.png");
        Sprite ring = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ComboCrew/Runtime/break_ring.png");
        Sprite bubble = LoadRushBubble();
        if (disk == null || ring == null || bubble == null)
            throw new InvalidOperationException("Sprites do balao ou da paciencia circular ausentes.");
        Transform character = (Transform)data.FindProperty("character").objectReferenceValue;
        GameObject balloon = (GameObject)data.FindProperty("balloon").objectReferenceValue;
        Image bubbleImage = balloon.GetComponent<Image>();
        bubbleImage.sprite = bubble;
        bubbleImage.type = Image.Type.Simple;
        bubbleImage.color = Color.white;
        bubbleImage.preserveAspect = false;
        bubbleImage.rectTransform.anchoredPosition = new Vector2(0f, 125f);
        bubbleImage.rectTransform.sizeDelta = new Vector2(200f, 100f);
        foreach (string oldPart in new[] { "CorpoBalao", "PontaBalao" })
        {
            Transform part = balloon.transform.Find(oldPart);
            if (part != null) Undo.DestroyObjectImmediate(part.gameObject);
        }
        CanvasGroup group = EditorSceneUI.GetOrAdd<CanvasGroup>(balloon);
        group.alpha = 1f;
        data.FindProperty("balloonGroup").objectReferenceValue = group;
        data.FindProperty("orderIconSpacing").floatValue = 56f;
        TMP_Text speech = (TMP_Text)data.FindProperty("speech").objectReferenceValue;
        speech.color = Color.white;
        speech.fontStyle = FontStyles.Bold;
        speech.fontSize = 20f;
        speech.rectTransform.anchoredPosition = new Vector2(0f, 7f);
        speech.rectTransform.sizeDelta = new Vector2(156f, 64f);
        Transform items = balloon.transform.Find("ItensPedido");
        if (items != null) ((RectTransform)items).anchoredPosition = new Vector2(0f, 7f);
        SerializedProperty icons = data.FindProperty("itemIcons");
        EnlargeFoodIcons(icons, 60f);
        for (int i = 0; i < icons.arraySize; i++)
        {
            RectTransform icon = (RectTransform)((GameObject)icons.GetArrayElementAtIndex(i).objectReferenceValue).transform;
            icon.sizeDelta = new Vector2(56f, 56f);
            icon.anchoredPosition = new Vector2((i - 1) * 56f, 0f);
        }
        Image fill = (Image)data.FindProperty("patienceFill").objectReferenceValue;
        GameObject trackObject = (GameObject)data.FindProperty("patienceTrack").objectReferenceValue;
        RectTransform track = (RectTransform)trackObject.transform;
        track.SetParent(character, false);
        track.anchorMin = track.anchorMax = track.pivot = new Vector2(.5f, .5f);
        track.anchoredPosition = new Vector2(-110f, 0f);
        track.sizeDelta = new Vector2(52f, 52f);
        track.localScale = Vector3.one;
        track.SetAsLastSibling();
        Image background = track.GetComponent<Image>();
        background.sprite = disk;
        background.type = Image.Type.Simple;
        background.color = new Color(.12f, .12f, .12f);
        background.raycastTarget = false;
        RectTransform rect = fill.rectTransform;
        rect.SetParent(track, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(50f, 50f);
        rect.SetAsFirstSibling();
        fill.sprite = disk;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = true;
        fill.raycastTarget = false;
        Transform outline = track.Find("ContornoPreto");
        if (outline == null)
        {
            outline = new GameObject("ContornoPreto", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
            outline.SetParent(track, false);
        }
        RectTransform border = (RectTransform)outline;
        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        border.offsetMin = new Vector2(-6f, -6f);
        border.offsetMax = new Vector2(6f, 6f);
        border.SetAsLastSibling();
        Image image = border.GetComponent<Image>();
        image.sprite = ring;
        image.type = Image.Type.Simple;
        image.color = Color.black;
        image.raycastTarget = false;
        data.FindProperty("circularPatience").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite LoadRushBubble()
    {
        const string path = "Assets/Art/ComboCrew/Polish/rush_speech_bubble.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Imagem do balao Rush Balance ausente.");
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
#pragma warning disable 0618
            importer.spritesheet = new[] { new SpriteMetaData {
                name = "rush_speech_bubble", rect = new Rect(294, 82, 1464, 737),
                pivot = new Vector2(.5f, .5f), alignment = (int)SpriteAlignment.Center } };
#pragma warning restore 0618
            importer.SaveAndReimport();
        }
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite) return sprite;
        throw new InvalidOperationException("Sprite do balao Rush Balance nao foi importado.");
    }

    private static void ConfigureBar(Image fill, RectTransform track, float height,
        Sprite trackSprite, Sprite fillSprite, Sprite outlineSprite)
    {
        float width = track.sizeDelta.x;
        track.sizeDelta = new Vector2(width, height);
        Image background = track.GetComponent<Image>();
        background.sprite = trackSprite;
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        background.raycastTarget = false;
        RectTransform rect = fill.rectTransform;
        rect.SetParent(track, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(0f, .5f);
        rect.anchoredPosition = new Vector2(-width * .5f + 1f, 0f);
        rect.sizeDelta = new Vector2((width - 2f) * .58f, height - 2f);
        rect.SetAsFirstSibling();
        fill.sprite = fillSprite;
        fill.type = Image.Type.Sliced;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.raycastTarget = false;
        Transform outline = track.Find("ContornoPreto");
        if (outline == null)
        {
            outline = new GameObject("ContornoPreto", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
            outline.SetParent(track, false);
        }
        RectTransform border = (RectTransform)outline;
        border.anchorMin = Vector2.zero;
        border.anchorMax = Vector2.one;
        border.offsetMin = border.offsetMax = Vector2.zero;
        border.SetAsLastSibling();
        Image image = border.GetComponent<Image>();
        image.sprite = outlineSprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static void Collect(GameObject go, List<GameObject> objects)
    {
        objects.Add(go);
        foreach (Transform child in go.transform) Collect(child.gameObject, objects);
    }

    private static List<T> FindAll<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return result;
    }

    private static void EnlargeFoodIcons(SerializedProperty icons, float size)
    {
        for (int i = 0; i < icons.arraySize; i++)
        {
            GameObject root = (GameObject)icons.GetArrayElementAtIndex(i).objectReferenceValue;
            if (root == null || root.transform.childCount == 0) continue;
            Transform art = root.transform.GetChild(0);
            if (art is RectTransform imageRect)
            {
                imageRect.sizeDelta = new Vector2(size, size);
                imageRect.anchoredPosition = Vector2.zero;
                Image image = art.GetComponent<Image>();
                if (image != null) image.preserveAspect = true;
            }
            for (int child = 1; child < root.transform.childCount; child++)
                root.transform.GetChild(child).gameObject.SetActive(false);
        }
    }
}

// Reutiliza os ajustes serializados da cena: cada escolha mantém sua própria transformação.
[CustomEditor(typeof(RushBalanceController))]
internal sealed class RushBalanceControllerEditor : Editor
{
    private RushBalanceController Controller => (RushBalanceController)target;

    private void OnEnable()
    {
        Undo.undoRedoPerformed += RefreshPreview;
        Controller.ApplyHatPreviewInEditor();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= RefreshPreview;
    }

    private void RefreshPreview()
    {
        if (target == null || Application.isPlaying) return;
        serializedObject.Update();
        Controller.ApplyHatPreviewInEditor();
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("POSICIONAR CADA CHAPÉU — RUSH BALANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Escolha um chapéu e ajuste sua posição, tamanho e rotação abaixo ou na Scene View (W/E/R). Cada chapéu guarda seu próprio ajuste nesta cena. Salve a cena com Ctrl+S.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("previewHatInEditor"), new GUIContent("Mostrar prévia"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("editHatDirectlyInScene"), new GUIContent("Editar na Scene View"));
            SerializedProperty hats = serializedObject.FindProperty("npcHatOptions");
            SerializedProperty chosen = serializedObject.FindProperty("hatPreviewIndex");
            if (hats.arraySize > 0)
            {
                var labels = new string[hats.arraySize];
                for (int i = 0; i < labels.Length; i++)
                {
                    var sprite = hats.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    var catalog = serializedObject.FindProperty("customizationCatalog").objectReferenceValue as AvatarCustomizationCatalog;
                    var item = catalog != null ? catalog.GetItem(AvatarCustomizationCategory.Hat, i) : null;
                    labels[i] = i + " — " + (item != null ? item.DisplayName : sprite != null ? ObjectNames.NicifyVariableName(sprite.name) : "Sem chapéu");
                }
                chosen.intValue = EditorGUILayout.Popup("Chapéu mostrado na prévia", Mathf.Clamp(chosen.intValue, 0, labels.Length - 1), labels);
                SerializedProperty adjustments = serializedObject.FindProperty("rushHatAdjustments");
                if (chosen.intValue > 0 && chosen.intValue < adjustments.arraySize)
                {
                    SerializedProperty adjustment = adjustments.GetArrayElementAtIndex(chosen.intValue);
                    EditorGUILayout.PropertyField(adjustment.FindPropertyRelative("Offset"), new GUIContent("Deslocamento no Rush"));
                    EditorGUILayout.PropertyField(adjustment.FindPropertyRelative("ScaleMultiplier"), new GUIContent("Escala no Rush"));
                    EditorGUILayout.PropertyField(adjustment.FindPropertyRelative("Rotation"), new GUIContent("Rotação no Rush"));
                }
            }
            if (serializedObject.ApplyModifiedProperties()) Controller.ApplyHatPreviewInEditor();
            if (GUILayout.Button("Enquadrar chapéu na Scene View"))
            {
                Controller.ApplyHatPreviewInEditor();
                RectTransform hat = Controller.HatPreviewTransform;
                if (hat != null && SceneView.lastActiveSceneView != null)
                {
                    Vector3[] corners = new Vector3[4];
                    hat.GetWorldCorners(corners);
                    Bounds bounds = new Bounds(corners[0], Vector3.zero);
                    for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(corners[i]);
                    SceneView.lastActiveSceneView.Frame(bounds, false);
                }
            }
        }
        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "rushHatAdjustments", "hatPreviewIndex", "previewHatInEditor", "editHatDirectlyInScene");
        if (serializedObject.ApplyModifiedProperties()) Controller.ApplyHatPreviewInEditor();
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying) return;
        serializedObject.Update();
        if (!serializedObject.FindProperty("previewHatInEditor").boolValue
            || !serializedObject.FindProperty("editHatDirectlyInScene").boolValue
            || serializedObject.FindProperty("hatPreviewIndex").intValue == 0) return;
        RectTransform hat = Controller.HatPreviewTransform;
        if (hat == null) return;

        EditorGUI.BeginChangeCheck();
        Vector3 position = hat.position;
        Quaternion rotation = hat.rotation;
        Vector3 scale = hat.localScale;
        if (Tools.current == Tool.Move || Tools.current == Tool.Rect)
            position = Handles.PositionHandle(position, hat.parent != null ? hat.parent.rotation : Quaternion.identity);
        else if (Tools.current == Tool.Rotate)
            rotation = Handles.RotationHandle(rotation, position);
        else if (Tools.current == Tool.Scale)
            scale = Handles.ScaleHandle(scale, position, rotation, HandleUtility.GetHandleSize(position));
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(hat, "Posicionar chapéu no Rush Balance");
        hat.position = position;
        if (hat.parent != null) hat.localPosition = new Vector3(hat.localPosition.x, hat.localPosition.y, 0f);
        hat.rotation = rotation;
        hat.localRotation = Quaternion.Euler(0f, 0f, hat.localEulerAngles.z);
        float uniformScale = !Mathf.Approximately(scale.x, hat.localScale.x) ? scale.x : scale.y;
        hat.localScale = new Vector3(Mathf.Max(.01f, uniformScale), Mathf.Max(.01f, uniformScale), 1f);
        Controller.SaveRushHatAdjustment();
        Controller.ApplyHatPreviewInEditor();
        Repaint();
    }
}

[CustomEditor(typeof(AvatarView))]
internal sealed class AvatarViewEditor : Editor
{
    private AvatarView Avatar => (AvatarView)target;
    private void OnEnable() { Undo.undoRedoPerformed += Refresh; }
    private void OnDisable() { Undo.undoRedoPerformed -= Refresh; }
    private void Refresh()
    {
        if (target == null || Application.isPlaying) return;
        serializedObject.Update(); Avatar.AtualizarPreviaNoEditor();
        SceneView.RepaintAll(); Repaint();
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (!serializedObject.FindProperty("usarAjustesDeChapeuDaCena").boolValue)
        {
            DrawDefaultInspector(); return;
        }
        EditorGUILayout.LabelField("POSICIONAR CADA CHAPÉU — NESTA CENA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Os 22 chapéus usam o encaixe do catálogo mais um ajuste individual nesta cena. O rosto usa o centro do corpo. Salve a cena após editar.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("chapeuNaPrevia"), new GUIContent("Chapéu mostrado na prévia"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("editarChapeuDiretamenteNaScene"), new GUIContent("Editar na Scene View"));
            int index = serializedObject.FindProperty("chapeuNaPrevia").enumValueIndex;
            SerializedProperty adjustments = serializedObject.FindProperty("ajustesDosChapeusNaCena");
            if (index > 0 && index < adjustments.arraySize)
            {
                SerializedProperty item = adjustments.GetArrayElementAtIndex(index);
                EditorGUILayout.PropertyField(item.FindPropertyRelative("Offset"), new GUIContent("Deslocamento nesta cena"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("ScaleMultiplier"), new GUIContent("Escala nesta cena"));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("Rotation"), new GUIContent("Rotação nesta cena"));
            }
            if (serializedObject.ApplyModifiedProperties()) Refresh();
            if (GUILayout.Button("Selecionar chapéu para mover na Scene View"))
            {
                Avatar.AtualizarPreviaNoEditor();
                if (Avatar.HatPreviewTransform != null)
                    Selection.activeGameObject = Avatar.HatPreviewTransform.gameObject;
            }
        }
        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "chapeuNaPrevia", "ajustesDosChapeusNaCena", "editarChapeuDiretamenteNaScene");
        if (serializedObject.ApplyModifiedProperties()) Refresh();
    }
}
