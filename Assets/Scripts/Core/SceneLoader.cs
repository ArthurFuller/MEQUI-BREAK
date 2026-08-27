using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Central scene navigation entry point.
///
/// The existing Load(string) API is intentionally preserved so scene callbacks
/// already configured in the Inspector keep working.
///
/// Global navigation transition:
/// - Forward: current screen exits left, next screen enters from the right.
/// - Back: current screen exits right, previous screen enters from the left.
///
/// The transition is applied to every navigation performed through Load(string).
/// Boot -> Login remains an immediate startup load because AppBootstrapper owns it
/// and there is no previous application screen to animate out.
/// </summary>
public sealed class SceneLoader : MonoBehaviour
{
    private const string TransitionRootName = "__SceneTransitionRoot";

    [Header("Scene Transition - Slide")]
    [Tooltip("Scale used while a screen is travelling. 0.97 means a subtle 3% reduction.")]
    [SerializeField, Range(0.90f, 1f)]
    private float transitionScale = 0.97f;

    [Tooltip("Time, in seconds, for the current screen to shrink from 1 to Transition Scale.")]
    [SerializeField, Min(0.01f)]
    private float scaleOutDuration = 0.14f;

    [Tooltip("Time, in seconds, for the horizontal slide between the two scenes.")]
    [SerializeField, Min(0.01f)]
    private float slideDuration = 0.45f;

    [Tooltip("Time, in seconds, for the incoming screen to grow from Transition Scale back to 1.")]
    [SerializeField, Min(0.01f)]
    private float scaleInDuration = 0.22f;

    [Tooltip("Pause after the current screen finishes shrinking and before the horizontal slide begins.")]
    [SerializeField, Min(0f)]
    private float preSlidePauseDuration = 0.10f;

    [Tooltip("Optional pause after the incoming screen reaches the center and before it grows back to full size.")]
    [SerializeField, Min(0f)]
    private float postSlidePauseDuration = 0.03f;

    [Header("Scene Transition - Easing")]
    [SerializeField]
    private Ease scaleOutEase = Ease.OutQuad;

    [SerializeField]
    private Ease slideEase = Ease.InOutCubic;

    [SerializeField]
    private Ease scaleInEase = Ease.OutCubic;

    private static bool isTransitioning;
    private static readonly List<string> navigationHistory = new();

    /// <summary>True while two scenes are being prepared/animated.</summary>
    public static bool IsTransitionInProgress => isTransitioning;

    private enum TransitionDirection
    {
        Forward,
        Back
    }

    public void Load(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || isTransitioning)
            return;

        string targetSceneName = ResolveBuildSceneName(sceneName.Trim());
        Scene currentScene = SceneManager.GetActiveScene();

        if (!currentScene.IsValid() || !currentScene.isLoaded)
        {
            SceneManager.LoadScene(targetSceneName);
            return;
        }

        if (string.Equals(currentScene.name, targetSceneName, StringComparison.OrdinalIgnoreCase))
        {
            ReloadCurrentScene();
            return;
        }

        TransitionDirection direction = ResolveDirection(currentScene.name, targetSceneName);

        if (ShouldUseSlideTransition(currentScene.name, targetSceneName))
        {
            StartCoroutine(LoadWithSlideTransition(currentScene, targetSceneName, direction));
            return;
        }

        // Fallback for a scene that cannot participate in the additive transition
        // (for example, an invalid/not-in-build target). Normal loading behavior is
        // preserved rather than leaving navigation stuck.
        CommitNavigation(currentScene.name, targetSceneName, direction);
        SceneManager.LoadScene(targetSceneName);
    }

    public void ReloadCurrentScene()
    {
        if (isTransitioning)
            return;

        Scene current = SceneManager.GetActiveScene();
        if (current.IsValid())
            SceneManager.LoadScene(current.buildIndex);
    }

    private IEnumerator LoadWithSlideTransition(
        Scene currentScene,
        string targetSceneName,
        TransitionDirection direction)
    {
        isTransitioning = true;

        // Stop the outgoing screen from receiving a second click while the next
        // scene is being prepared. The current AudioListener stays enabled during
        // background loading so there is no audio gap.
        SetEventSystemsEnabled(currentScene, false);

        List<RectTransform> outgoingRoots = GetOrCreateTransitionRoots(currentScene);
        ResetRoots(outgoingRoots);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
            targetSceneName,
            LoadSceneMode.Additive);

        if (loadOperation == null)
        {
            SetEventSystemsEnabled(currentScene, true);
            isTransitioning = false;
            yield break;
        }

        // Preload to 90% while the outgoing scene remains fully active. Right
        // before activation, disable its AudioListener so the incoming scene can
        // enable its own listener without producing a duplicate-listener warning.
        loadOperation.allowSceneActivation = false;
        while (loadOperation.progress < 0.9f)
            yield return null;

        SetAudioListenersEnabled(currentScene, false);
        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
            yield return null;

        Scene incomingScene = FindLoadedScene(targetSceneName);
        if (!incomingScene.IsValid() || !incomingScene.isLoaded)
        {
            SetEventSystemsEnabled(currentScene, true);
            SetAudioListenersEnabled(currentScene, true);
            isTransitioning = false;
            yield break;
        }

        // The newly loaded EventSystem is also disabled until motion is finished.
        SetEventSystemsEnabled(incomingScene, false);

        List<RectTransform> incomingRoots = GetOrCreateTransitionRoots(incomingScene);
        Canvas.ForceUpdateCanvases();

        float incomingSide = direction == TransitionDirection.Forward ? 1f : -1f;
        float outgoingSide = -incomingSide;

        PrepareIncomingRoots(incomingRoots, incomingSide);

        Sequence transition = DOTween.Sequence()
            .SetUpdate(true)
            .SetAutoKill(true);

        // Keep the phases intentionally separate so the motion feels centered
        // and symmetrical:
        // 1) outgoing screen shrinks in place;
        // 2) short hold while both screens remain at Transition Scale;
        // 3) both screens slide at the same fixed scale;
        // 4) incoming screen grows only after it is fully centered.
        float slideStartTime = scaleOutDuration + preSlidePauseDuration;
        float scaleInStartTime = slideStartTime + slideDuration + postSlidePauseDuration;

        foreach (RectTransform root in outgoingRoots)
        {
            if (root == null)
                continue;

            transition.Insert(
                0f,
                root.DOScale(transitionScale, scaleOutDuration)
                    .SetEase(scaleOutEase));

            float width = GetRootWidth(root);
            transition.Insert(
                slideStartTime,
                root.DOAnchorPosX(outgoingSide * width, slideDuration)
                    .SetEase(slideEase));
        }

        // Incoming screen stays at Transition Scale for the whole horizontal
        // movement. It only returns to 1 after arriving at the exact center.
        foreach (RectTransform root in incomingRoots)
        {
            if (root == null)
                continue;

            transition.Insert(
                slideStartTime,
                root.DOAnchorPosX(0f, slideDuration)
                    .SetEase(slideEase));

            transition.Insert(
                scaleInStartTime,
                root.DOScale(1f, scaleInDuration)
                    .SetEase(scaleInEase));
        }

        yield return transition.WaitForCompletion();

        ResetRoots(incomingRoots);
        SceneManager.SetActiveScene(incomingScene);

        SetEventSystemsEnabled(incomingScene, true);

        CommitNavigation(currentScene.name, targetSceneName, direction);
        isTransitioning = false;

        // Nothing after this point depends on this SceneLoader instance because it
        // belongs to the scene that is about to be destroyed.
        SceneManager.UnloadSceneAsync(currentScene);
    }

    private static bool ShouldUseSlideTransition(string currentScene, string targetScene)
    {
        if (string.IsNullOrWhiteSpace(currentScene) || string.IsNullOrWhiteSpace(targetScene))
            return false;

        if (string.Equals(currentScene, targetScene, StringComparison.OrdinalIgnoreCase))
            return false;

        // Only scenes present in Build Settings can be preloaded additively.
        return Application.CanStreamedLevelBeLoaded(targetScene);
    }

    private static TransitionDirection ResolveDirection(string currentScene, string targetScene)
    {
        EnsureCurrentInHistory(currentScene);

        // The history is the strongest signal: if the requested scene already
        // exists behind the current one, this is a real Back navigation. This
        // correctly handles Profile -> Customization -> Profile as well as
        // paths where Customization was opened directly from HUB.
        int targetIndex = FindLastHistoryIndex(targetScene);
        if (targetIndex >= 0 && targetIndex < navigationHistory.Count - 1)
            return TransitionDirection.Back;

        // Some screens expose an explicit Back-to-HUB action. Keep its direction
        // deterministic even when that scene was launched directly in the Editor
        // and therefore has no usable navigation history yet.
        if (IsExplicitBackToHub(currentScene, targetScene))
            return TransitionDirection.Back;

        return TransitionDirection.Forward;
    }

    private static bool IsExplicitBackToHub(string currentScene, string targetScene)
    {
        if (!IsHub(targetScene))
            return false;

        return IsProfile(currentScene)
            || IsScene(currentScene, "Customization")
            || IsScene(currentScene, "Settings")
            || IsScene(currentScene, "Minigames")
            || IsScene(currentScene, "EnergyStation")
            || IsScene(currentScene, "Result");
    }

    private static void CommitNavigation(
        string currentScene,
        string targetScene,
        TransitionDirection direction)
    {
        EnsureCurrentInHistory(currentScene);

        if (direction == TransitionDirection.Back)
        {
            int targetIndex = FindLastHistoryIndex(targetScene);
            if (targetIndex >= 0)
            {
                navigationHistory.RemoveRange(
                    targetIndex + 1,
                    navigationHistory.Count - targetIndex - 1);
                return;
            }
        }

        if (navigationHistory.Count == 0 ||
            !string.Equals(
                navigationHistory[navigationHistory.Count - 1],
                targetScene,
                StringComparison.OrdinalIgnoreCase))
        {
            navigationHistory.Add(targetScene);
        }
    }

    private static void EnsureCurrentInHistory(string currentScene)
    {
        if (string.IsNullOrWhiteSpace(currentScene))
            return;

        if (navigationHistory.Count == 0)
        {
            navigationHistory.Add(currentScene);
            return;
        }

        if (!string.Equals(
                navigationHistory[navigationHistory.Count - 1],
                currentScene,
                StringComparison.OrdinalIgnoreCase))
        {
            navigationHistory.Add(currentScene);
        }
    }

    private static int FindLastHistoryIndex(string sceneName)
    {
        for (int i = navigationHistory.Count - 1; i >= 0; i--)
        {
            if (string.Equals(
                    navigationHistory[i],
                    sceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<RectTransform> GetOrCreateTransitionRoots(Scene scene)
    {
        List<RectTransform> roots = new();
        HashSet<Canvas> processedCanvases = new();

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Canvas[] canvases = rootObject.GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || processedCanvases.Contains(canvas))
                    continue;

                // A nested Canvas will travel together with its parent root Canvas.
                if (canvas.rootCanvas != canvas)
                    continue;

                processedCanvases.Add(canvas);

                RectTransform transitionRoot = FindOrCreateTransitionRoot(canvas);
                if (transitionRoot != null)
                    roots.Add(transitionRoot);
            }
        }

        return roots;
    }

    private static RectTransform FindOrCreateTransitionRoot(Canvas canvas)
    {
        if (canvas == null)
            return null;

        Transform existing = canvas.transform.Find(TransitionRootName);
        if (existing is RectTransform existingRect)
            return existingRect;

        List<Transform> originalChildren = new();
        for (int i = 0; i < canvas.transform.childCount; i++)
            originalChildren.Add(canvas.transform.GetChild(i));

        GameObject rootObject = new(TransitionRootName, typeof(RectTransform));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;
        root.anchoredPosition = Vector2.zero;
        root.SetSiblingIndex(0);

        // The wrapper has exactly the same rect as the Canvas. Keeping local UI
        // values while reparenting therefore preserves the existing layout.
        foreach (Transform child in originalChildren)
            child.SetParent(root, false);

        return root;
    }

    private void PrepareIncomingRoots(List<RectTransform> roots, float side)
    {
        foreach (RectTransform root in roots)
        {
            if (root == null)
                continue;

            root.DOKill();
            root.localScale = Vector3.one * transitionScale;
            root.anchoredPosition = new Vector2(side * GetRootWidth(root), 0f);
        }
    }

    private static void ResetRoots(List<RectTransform> roots)
    {
        foreach (RectTransform root in roots)
        {
            if (root == null)
                continue;

            root.DOKill();
            root.anchoredPosition = Vector2.zero;
            root.localScale = Vector3.one;
        }
    }

    private static float GetRootWidth(RectTransform root)
    {
        if (root == null)
            return Mathf.Max(Screen.width, 1f);

        float width = root.rect.width;
        if (width <= 0.01f && root.parent is RectTransform parent)
            width = parent.rect.width;

        if (width <= 0.01f)
            width = Screen.width;

        return Mathf.Max(width, 1f);
    }

    private static void SetEventSystemsEnabled(Scene scene, bool enabled)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            EventSystem[] eventSystems = rootObject.GetComponentsInChildren<EventSystem>(true);
            foreach (EventSystem eventSystem in eventSystems)
            {
                if (eventSystem != null)
                    eventSystem.enabled = enabled;
            }
        }
    }

    private static void SetAudioListenersEnabled(Scene scene, bool enabled)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            AudioListener[] listeners = rootObject.GetComponentsInChildren<AudioListener>(true);
            foreach (AudioListener listener in listeners)
            {
                if (listener != null)
                    listener.enabled = enabled;
            }
        }
    }

    private static Scene FindLoadedScene(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded &&
                string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return scene;
            }
        }

        return default;
    }

    private static string ResolveBuildSceneName(string requestedSceneName)
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(
                    sceneName,
                    requestedSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return sceneName;
            }
        }

        return requestedSceneName;
    }

    private static bool IsHub(string sceneName) =>
        string.Equals(sceneName, "Hub", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sceneName, "HUB", StringComparison.OrdinalIgnoreCase);

    private static bool IsProfile(string sceneName) =>
        IsScene(sceneName, "Profile");

    private static bool IsScene(string sceneName, string expectedName) =>
        string.Equals(sceneName, expectedName, StringComparison.OrdinalIgnoreCase);
}
