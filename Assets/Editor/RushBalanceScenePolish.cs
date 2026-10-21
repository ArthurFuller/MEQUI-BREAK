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
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RushBalance] Acabamento visual aplicado e salvo na cena.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
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
