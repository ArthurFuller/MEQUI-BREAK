#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Repairs an already-open scene after an external scene edit. Editor only:
// the highlight remains a serialized, manually editable child of each slot.
[InitializeOnLoad]
public static class CustomizationSelectionHierarchyInstaller
{
    private const string ScenePath = "Assets/Scenes/Customization/Customization.unity";
    private const string MaterialPath = "Assets/Art/Customization/SlotSelectionOutline.mat";

    static CustomizationSelectionHierarchyInstaller()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || Application.isBatchMode)
            return;
        EditorApplication.delayCall += SynchronizeOpenScene;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += SynchronizeOpenScene;
        };
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        Synchronize(scene);
    }

    [MenuItem("Mequi Break/Customização/Sincronizar destaques na Hierarchy")]
    public static void SynchronizeOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded)
            Synchronize(scene);
    }

    public static int Synchronize(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            return 0;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogError("Material de destaque da customização não encontrado: " + MaterialPath);
            return 0;
        }

        int repaired = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (AvatarOptionButton option in root.GetComponentsInChildren<AvatarOptionButton>(true))
        {
            var serialized = new SerializedObject(option);
            SerializedProperty reference = serialized.FindProperty("selectionHighlight");
            if (reference == null || reference.objectReferenceValue != null)
                continue;
            Image slot = option.GetComponent<Image>();
            if (slot == null)
                continue;

            Transform child = option.transform.Find("DestaqueSelecao");
            GameObject highlight;
            if (child != null)
                highlight = child.gameObject;
            else
            {
                highlight = new GameObject("DestaqueSelecao", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(highlight, "Adicionar destaque de seleção");
                highlight.transform.SetParent(option.transform, false);
                highlight.layer = option.gameObject.layer;
                RectTransform rect = highlight.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                Image image = highlight.GetComponent<Image>();
                image.sprite = slot.sprite;
                image.type = slot.type;
                image.pixelsPerUnitMultiplier = slot.pixelsPerUnitMultiplier;
                image.material = material;
                image.color = new Color(1f, 0.7176471f, 0f, 1f);
                image.raycastTarget = false;
                highlight.SetActive(false);
            }

            Button button = option.GetComponent<Button>();
            if (button != null && button.transition != Selectable.Transition.None)
            {
                Undo.RecordObject(button, "Preservar cores manuais do slot");
                button.transition = Selectable.Transition.None;
            }
            Undo.RecordObject(option, "Vincular destaque de seleção");
            reference.objectReferenceValue = highlight;
            serialized.ApplyModifiedProperties();
            repaired++;
        }

        if (repaired > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Customização: {repaired} destaques vinculados na cena aberta. Salve a cena para manter a configuração.");
        }
        return repaired;
    }
}

// Mantém também a cena já aberta em sincronia, sem substituir edições do usuário.
[InitializeOnLoad]
public static class AvatarContourHierarchyRepair
{
    private const string BodyPath = "Assets/Art/Characters/AvatarCustomization/body_base.png";
    static AvatarContourHierarchyRepair()
    {
        if (AssetDatabase.IsAssetImportWorkerProcess() || Application.isBatchMode) return;
        EditorApplication.delayCall += SynchronizeOpenScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => SynchronizeOpenScenes();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += SynchronizeOpenScenes;
        };
    }
    [MenuItem("Mequi Break/Avatar/Sincronizar contornos nas cenas abertas")]
    public static void SynchronizeOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        int repaired = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            repaired += Synchronize(SceneManager.GetSceneAt(i));
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
            "../tmp/avatar-contour-repair"));
        System.IO.Directory.CreateDirectory(folder);
        string report = "Synchronized open scenes. Changes: " + repaired;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (AvatarView avatar in root.GetComponentsInChildren<AvatarView>(true))
            {
                var data = new SerializedObject(avatar);
                foreach (string field in new[] { "bodyImage", "hatImage" })
                {
                    Image image = ImageReference(data, field);
                    AvatarSpriteOutline outline = image != null ? image.GetComponent<AvatarSpriteOutline>() : null;
                    report += "\n" + scene.name + "/" + avatar.name + "/" + field
                        + ": outline=" + (outline != null && outline.enabled)
                        + ", width=" + (outline != null ? outline.Thickness : 0f)
                        + ", active=" + (image != null && image.gameObject.activeInHierarchy);
                }
            }
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "editor-sync.txt"), report);
    }
    public static int Synchronize(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return 0;
        int changed = CustomizationPurchasePreviewInstaller.Synchronize(scene);
        bool combo = scene.path == "Assets/Scenes/ComboCrew/ComboCrew.unity";
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (AvatarView avatar in root.GetComponentsInChildren<AvatarView>(true))
            {
                var data = new SerializedObject(avatar);
                changed += Ensure(ImageReference(data, "bodyImage"), combo ? 3.5f : 10f);
                changed += Ensure(ImageReference(data, "hatImage"), combo ? 3.5f : 10f);
                if (combo && data.FindProperty("fallbackHatIndex").intValue != 1)
                {
                    Undo.RecordObject(avatar, "Restaurar chapéu do jogador");
                    data.FindProperty("fallbackHatIndex").intValue = 1;
                    data.ApplyModifiedProperties(); changed++;
                }
                avatar.AtualizarPreviaNoEditor();
            }
            foreach (AvatarOptionButton option in root.GetComponentsInChildren<AvatarOptionButton>(true))
                foreach (Image image in option.GetComponentsInChildren<Image>(true))
                    if (image.name.StartsWith("Preview_Hat_") || image.name.StartsWith("Preview_Color_"))
                    { changed += Ensure(image, 4f); AvatarView.StyleCustomizationIcon(image, image.name.StartsWith("Preview_Hat_") ? AvatarCustomizationCategory.Hat : AvatarCustomizationCategory.Color); }
                    else if (image.name.StartsWith("Preview_Face_")) AvatarView.StyleCustomizationIcon(image, AvatarCustomizationCategory.Face);
            if (!combo) continue;
            Sprite silhouette = AssetDatabase.LoadAssetAtPath<Sprite>(BodyPath);
            foreach (ComboCrewWorkerView worker in root.GetComponentsInChildren<ComboCrewWorkerView>(true))
            {
                var data = new SerializedObject(worker);
                var actor = data.FindProperty("actor").objectReferenceValue as RectTransform;
                if (actor == null) continue;
                bool player = data.FindProperty("playerAvatar").objectReferenceValue != null;
                Image body = actor.Find("Corpo")?.GetComponent<Image>();
                if (!player && body != null && silhouette != null && body.sprite != silhouette)
                {
                    Color tint = CenterColor(body.sprite);
                    Undo.RecordObject(body, "Uniformizar corpo do funcionário");
                    body.sprite = silhouette; body.color = tint; changed++;
                }
                if (data.FindProperty("bodySilhouette").objectReferenceValue != silhouette)
                {
                    Undo.RecordObject(worker, "Vincular silhueta do funcionário");
                    data.FindProperty("bodySilhouette").objectReferenceValue = silhouette;
                    data.ApplyModifiedProperties(); changed++;
                }
                changed += Ensure(body, 3.5f);
            }
            foreach (ComboCrewWorkerCard card in root.GetComponentsInChildren<ComboCrewWorkerCard>(true))
            {
                var data = new SerializedObject(card);
                GameObject details = data.FindProperty("cardDetails").objectReferenceValue as GameObject;
                GameObject drag = data.FindProperty("portraitWhileDragging").objectReferenceValue as GameObject;
                if (details != null)
                {
                    changed += Ensure(details.transform.Find("CorpoMiniatura")?.GetComponent<Image>(), 3.5f);
                    changed += Ensure(details.transform.Find("ChapeuMiniatura")?.GetComponent<Image>(), 3.5f);
                }
                if (drag != null)
                {
                    changed += Ensure(drag.GetComponent<Image>(), 3.5f);
                    changed += Ensure(drag.transform.Find("ChapeuNoArraste")?.GetComponent<Image>(), 3.5f);
                }
                card.RefreshPortrait();
            }
        }
        if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
        return changed;
    }
    private static Image ImageReference(SerializedObject data, string name) =>
        data.FindProperty(name).objectReferenceValue as Image;
    private static int Ensure(Image image, float width)
    {
        if (image == null) return 0;
        AvatarSpriteOutline outline = image.GetComponent<AvatarSpriteOutline>();
        int changed = 0;
        if (outline == null) { outline = Undo.AddComponent<AvatarSpriteOutline>(image.gameObject); changed++; }
        if (!outline.enabled || Mathf.Abs(outline.Thickness - width) > .001f)
        {
            Undo.RecordObject(outline, "Ajustar contorno do avatar");
            outline.enabled = true; outline.Thickness = width; changed++;
        }
        return changed;
    }
    private static Color CenterColor(Sprite sprite)
    {
        if (sprite == null) return Color.white;
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0);
        Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(sprite.texture, temporary); RenderTexture.active = temporary;
            Rect area = sprite.textureRect;
            pixel.ReadPixels(new Rect(Mathf.Floor(area.center.x), Mathf.Floor(area.center.y), 1, 1), 0, 0);
            return pixel.GetPixel(0, 0);
        }
        finally
        {
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary);
            Object.DestroyImmediate(pixel);
        }
    }
}


// Montagem apenas no Editor. Durante o jogo, o controlador só troca a imagem e o preço.
public static class CustomizationPurchasePreviewInstaller
{
    public static int Synchronize(Scene scene)
    {
        if (scene.path != "Assets/Scenes/Customization/Customization.unity") return 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (CustomizationController controller in root.GetComponentsInChildren<CustomizationController>(true))
        {
            var data = new SerializedObject(controller);
            if (data.FindProperty("purchaseConfirmItem").objectReferenceValue != null
                && data.FindProperty("purchaseConfirmSlot").objectReferenceValue != null) continue;
            GameObject panel = data.FindProperty("purchaseConfirmPanel").objectReferenceValue as GameObject;
            TMPro.TMP_Text price = data.FindProperty("purchaseConfirmPrice").objectReferenceValue as TMPro.TMP_Text;
            if (panel == null || price == null) continue;
            Transform container = panel.transform.Find("Container");
            if (container == null) continue;
            Undo.RecordObject(container, "Atualizar conteúdo da compra");
            ((RectTransform)container).sizeDelta = new Vector2(160, 36);
            VerticalLayoutGroup vertical = container.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
            {
                Undo.RecordObject(vertical, "Ajustar conteúdo da compra");
                vertical.spacing = 14; vertical.childForceExpandHeight = false; vertical.childForceExpandWidth = false;
                vertical.childScaleHeight = true; vertical.childScaleWidth = true;
            }
            Transform title = container.Find("TitleText");
            if (title is RectTransform titleRect) titleRect.sizeDelta = new Vector2(560, 50);
            Transform buttons = container.Find("ButtonRow");
            if (buttons is RectTransform buttonsRect) buttonsRect.sizeDelta = new Vector2(377.1f, 69.6f);
            RectTransform row = Node("PurchaseItemRow", container, new Vector2(450, 160));
            HorizontalLayoutGroup horizontal = row.GetComponent<HorizontalLayoutGroup>();
            if (horizontal == null) horizontal = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
            horizontal.spacing = 32; horizontal.childAlignment = TextAnchor.MiddleCenter;
            horizontal.childControlWidth = horizontal.childControlHeight = false;
            horizontal.childForceExpandWidth = horizontal.childForceExpandHeight = false;
            row.SetSiblingIndex(1);
            RectTransform slotRect = Node("SlotGrid", row, new Vector2(160, 160));
            slotRect.SetAsFirstSibling();
            Image slot = slotRect.GetComponent<Image>() ?? Undo.AddComponent<Image>(slotRect.gameObject);
            Image sourceSlot = null, sourceItem = null;
            SerializedProperty options = data.FindProperty("hatOptions");
            for (int i = 0; i < options.arraySize; i++)
            {
                GameObject option = options.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (option == null) continue;
                if (sourceSlot == null) sourceSlot = option.GetComponent<Image>();
                foreach (Image image in option.GetComponentsInChildren<Image>(true))
                    if (image.name.StartsWith("Preview_Hat_") && image.sprite != null && sourceItem == null)
                        sourceItem = image;
            }
            if (sourceSlot != null) { slot.sprite = sourceSlot.sprite; slot.color = sourceSlot.color; slot.type = sourceSlot.type; }
            slot.raycastTarget = false;
            RectTransform itemRect = Node("PurchaseItem", slotRect, new Vector2(128, 128));
            Image item = itemRect.GetComponent<Image>() ?? Undo.AddComponent<Image>(itemRect.gameObject);
            if (sourceItem != null) { item.sprite = sourceItem.sprite; item.color = sourceItem.color; }
            item.preserveAspect = true; item.raycastTarget = false;
            AvatarSpriteOutline outline = item.GetComponent<AvatarSpriteOutline>()
                ?? Undo.AddComponent<AvatarSpriteOutline>(item.gameObject);
            outline.Thickness = 4;
            Undo.RecordObject(price, "Mostrar preço da compra");
            Undo.SetTransformParent(price.transform, row, "Posicionar preço ao lado do item");
            price.name = "PurchasePrice";
            RectTransform rect = price.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(220, 80);
            price.margin = Vector4.zero; price.text = "100 PB"; price.fontSize = 44;
            price.enableAutoSizing = true; price.fontSizeMin = 28; price.fontSizeMax = 44;
            price.alignment = TMPro.TextAlignmentOptions.MidlineLeft; price.raycastTarget = false;
            Undo.RecordObject(controller, "Vincular item e preço da compra");
            data.FindProperty("purchaseConfirmSlot").objectReferenceValue = slot;
            data.FindProperty("purchaseConfirmItem").objectReferenceValue = item;
            data.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            return 1;
        }
        return 0;
    }
    private static RectTransform Node(string name, Transform parent, Vector2 size)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Montar confirmação de compra");
            go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
            child = go.transform;
        }
        RectTransform rect = (RectTransform)child;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size;
        return rect;
    }
}

#endif
