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
#endif
