using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a very small staggered settle animation to the main content of a scene
/// after the global scene slide has finished. It intentionally uses position +
/// scale only (no alpha) and skips Customization, which owns its own option wave.
/// </summary>
public static class UIContentEntranceAnimator
{
    private const string TransitionRootName = "__SceneTransitionRoot";

    private static readonly Dictionary<int, Sequence> ActiveSequences = new();

    public static void Play(
        Scene scene,
        float startYOffset,
        float startScale,
        float duration,
        float stagger,
        Ease ease)
    {
        if (!scene.IsValid() || !scene.isLoaded || ShouldSkipScene(scene.name))
            return;

        startScale = Mathf.Clamp(startScale, 0.9f, 1f);
        duration = Mathf.Max(0.01f, duration);
        stagger = Mathf.Max(0f, stagger);

        Kill(scene);

        List<RectTransform> targets = CollectTargets(scene);
        if (targets.Count == 0)
            return;

        Sequence sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetAutoKill(true);

        for (int i = 0; i < targets.Count; i++)
        {
            RectTransform target = targets[i];
            if (target == null)
                continue;

            Vector2 finalPosition = target.anchoredPosition;
            Vector3 finalScale = target.localScale;

            target.DOKill();
            target.anchoredPosition = finalPosition + Vector2.up * startYOffset;
            target.localScale = finalScale * startScale;

            float delay = i * stagger;

            sequence.Insert(
                delay,
                target.DOAnchorPos(finalPosition, duration)
                    .SetEase(ease)
                    .SetUpdate(true));

            sequence.Insert(
                delay,
                target.DOScale(finalScale, duration)
                    .SetEase(ease)
                    .SetUpdate(true));
        }

        int handle = scene.handle;
        ActiveSequences[handle] = sequence;

        sequence.OnComplete(() =>
        {
            ActiveSequences.Remove(handle);
        });
    }

    public static void Kill(Scene scene)
    {
        if (!scene.IsValid())
            return;

        if (ActiveSequences.TryGetValue(scene.handle, out Sequence sequence) &&
            sequence != null && sequence.IsActive())
        {
            sequence.Kill(true);
        }

        ActiveSequences.Remove(scene.handle);
    }

    private static List<RectTransform> CollectTargets(Scene scene)
    {
        List<RectTransform> targets = new();
        HashSet<Canvas> processed = new();

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Canvas[] canvases = rootObject.GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || canvas.rootCanvas != canvas || !processed.Add(canvas))
                    continue;

                Transform contentParent = canvas.transform.Find(TransitionRootName) ?? canvas.transform;

                for (int i = 0; i < contentParent.childCount; i++)
                {
                    Transform child = contentParent.GetChild(i);
                    if (child is not RectTransform rect || !child.gameObject.activeInHierarchy)
                        continue;

                    if (ShouldSkipElement(scene.name, child.name))
                        continue;

                    targets.Add(rect);
                }
            }
        }

        return targets;
    }

    private static bool ShouldSkipScene(string sceneName)
    {
        // Customization already has a stronger, purpose-built option wave.
        return IsScene(sceneName, "Customization") || IsScene(sceneName, "Boot");
    }

    private static bool ShouldSkipElement(string sceneName, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return true;

        if (objectName.StartsWith("__", StringComparison.Ordinal))
            return true;

        if (string.Equals(objectName, "Background", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(objectName, "Header", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(objectName, "ResultPopup", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(objectName, "Avatar", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(objectName, "AvatarArea", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The HUB PB flight targets PointsLabel inside PlayerCard. Keep that card
        // fixed while a pending reward is about to animate into it.
        if (IsHub(sceneName) &&
            string.Equals(objectName, "PlayerCard", StringComparison.OrdinalIgnoreCase) &&
            PlayerManager.Instance != null &&
            PlayerManager.Instance.PendingBreakPoints > 0)
        {
            return true;
        }

        return false;
    }

    private static bool IsHub(string sceneName) =>
        IsScene(sceneName, "HUB") || IsScene(sceneName, "Hub");

    private static bool IsScene(string sceneName, string expected) =>
        string.Equals(sceneName, expected, StringComparison.OrdinalIgnoreCase);
}
