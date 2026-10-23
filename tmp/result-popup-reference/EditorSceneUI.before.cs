using UnityEngine;
using UnityEngine.SceneManagement;

internal static class EditorSceneUI
{
    internal static Transform Find(Scene scene, string path)
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

    internal static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    internal static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    internal static void Stretch(RectTransform rect) =>
        Stretch(rect, Vector2.zero, Vector2.zero);

    internal static void Stretch(RectTransform rect, Vector2 inset) =>
        Stretch(rect, inset, -inset);

    internal static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }
}