using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ComboCrewScenePreviewSetup
{
    [MenuItem("Combo Crew/Atualizar prévia visual _F8")]
    private static void Setup()
    {
        const string path = "Assets/Scenes/ComboCrew/ComboCrew.unity";
        if (EditorSceneManager.GetActiveScene().path != path)
        {
            Debug.LogError("Abra a cena ComboCrew antes de atualizar a prévia visual.");
            return;
        }

        var controller = Object.FindFirstObjectByType<ComboCrewController>(FindObjectsInactive.Include);
        if (controller == null) return;
        var data = new SerializedObject(controller);
        var stationArray = data.FindProperty("stations");
        var workerArray = data.FindProperty("workers");
        var preview = controller.GetComponent<ComboCrewScenePreview>();
        if (preview == null) preview = Undo.AddComponent<ComboCrewScenePreview>(controller.gameObject);
        var previewData = new SerializedObject(preview);
        var previewStations = previewData.FindProperty("stations");
        var previewWorkers = previewData.FindProperty("workers");
        previewStations.arraySize = stationArray.arraySize;
        previewWorkers.arraySize = workerArray.arraySize;

        // Jogador no balcão; os outros atores mostram uma distribuição possível.
        int[] previewOrder = { 3, 0, 1, 2, 4 };
        for (int i = 0; i < stationArray.arraySize; i++)
            previewStations.GetArrayElementAtIndex(i).objectReferenceValue =
                stationArray.GetArrayElementAtIndex(previewOrder[i]).objectReferenceValue;

        for (int i = 0; i < workerArray.arraySize; i++)
        {
            var worker = (ComboCrewWorkerView)workerArray.GetArrayElementAtIndex(i).objectReferenceValue;
            previewWorkers.GetArrayElementAtIndex(i).objectReferenceValue = worker;
            if (worker == null) continue;
            var workerData = new SerializedObject(worker);
            var left = workerData.FindProperty("breakBubble").objectReferenceValue as GameObject;
            var right = workerData.FindProperty("breakBubbleRight").objectReferenceValue as GameObject;
            if (left == null) continue;
            if (right == null)
            {
                right = Object.Instantiate(left, left.transform.parent);
                Undo.RegisterCreatedObjectUndo(right, "Criar balão direito");
                right.name = "BalaoDireita";
                var rect = (RectTransform)right.transform;
                rect.pivot = new Vector2(0f, .5f);
                rect.anchoredPosition = new Vector2(48f, 63f);
                var graphic = right.GetComponent<Image>();
                graphic.sprite = (Sprite)workerData.FindProperty("bubblePointingLeft").objectReferenceValue;
                var text = right.GetComponentInChildren<TMP_Text>(true);
                text.rectTransform.anchoredPosition = new Vector2(13f, 4f);
                workerData.FindProperty("breakBubbleRight").objectReferenceValue = right;
                workerData.FindProperty("breakBubbleRightText").objectReferenceValue = text;
                right.SetActive(false);
            }
            left.name = "BalaoEsquerda";
            workerData.ApplyModifiedProperties();
        }

        previewData.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorSceneManager.SaveScene(controller.gameObject.scene);
        Debug.Log("Combo Crew: prévia visual editável configurada na cena.");
    }
}
