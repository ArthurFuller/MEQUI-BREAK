using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Ponto central de navegação entre cenas.
///
/// A API Load(string) é preservada para manter os callbacks configurados no Inspector.
/// A única animação global é um deslizamento horizontal: no avanço, a cena atual
/// sai pela esquerda e a próxima entra pela direita; no retorno, o sentido é invertido.
/// </summary>
public sealed class SceneLoader : MonoBehaviour
{
    private const string TransitionRootName = "__SceneTransitionRoot";

    [Header("Transição de cena")]
    [Tooltip("Duração do deslizamento horizontal entre as cenas. Não é um atraso antes da troca.")]
    [SerializeField, Min(0.01f)]
    private float slideDuration = 0.45f;

    private static bool isTransitioning;
    private static readonly List<string> navigationHistory = new();
    private static readonly List<GameObject> rootObjectBuffer = new();
    private static readonly List<Canvas> canvasBuffer = new();
    private static readonly List<EventSystem> eventSystemBuffer = new();
    private static readonly List<AudioListener> audioListenerBuffer = new();

    /// <summary>Indica se uma troca de cena está sendo carregada ou animada.</summary>
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

        if (ShouldUseSlideTransition(targetSceneName))
        {
            StartCoroutine(LoadWithSlideTransition(currentScene, targetSceneName, direction));
            return;
        }

        // Mantém o carregamento comum para cenas que não estão no Build Settings.
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

        // Aguarda apenas o carregamento real; não existe espera adicional antes do slide.
        loadOperation.allowSceneActivation = false;
        while (loadOperation.progress < 0.9f)
            yield return null;

        // O AudioManager persistente mantém o listener durante a transição.
        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
            yield return null;

        Scene incomingScene = FindLoadedScene(targetSceneName);
        if (!incomingScene.IsValid() || !incomingScene.isLoaded)
        {
            SetEventSystemsEnabled(currentScene, true);
            isTransitioning = false;
            yield break;
        }

        SetAudioListenersEnabled(incomingScene, false);
        SetEventSystemsEnabled(incomingScene, false);

        List<RectTransform> incomingRoots = GetOrCreateTransitionRoots(incomingScene);
        Canvas.ForceUpdateCanvases();

        float incomingSide = direction == TransitionDirection.Forward ? 1f : -1f;
        float outgoingSide = -incomingSide;
        PrepareIncomingRoots(incomingRoots, incomingSide);

        Sequence transition = DOTween.Sequence()
            .SetUpdate(true)
            .SetAutoKill(true);

        foreach (RectTransform root in outgoingRoots)
        {
            if (root == null)
                continue;

            transition.Insert(
                0f,
                root.DOAnchorPosX(outgoingSide * GetRootWidth(root), slideDuration)
                    .SetEase(Ease.InOutCubic));
        }

        foreach (RectTransform root in incomingRoots)
        {
            if (root == null)
                continue;

            transition.Insert(
                0f,
                root.DOAnchorPosX(0f, slideDuration)
                    .SetEase(Ease.InOutCubic));
        }

        yield return transition.WaitForCompletion();

        ResetRoots(incomingRoots);
        SceneManager.SetActiveScene(incomingScene);
        SetEventSystemsEnabled(incomingScene, true);

        CommitNavigation(currentScene.name, targetSceneName, direction);
        isTransitioning = false;

        // A cena anterior deixa de participar da navegação após a animação.
        SceneManager.UnloadSceneAsync(currentScene);
    }

    private static bool ShouldUseSlideTransition(string targetScene)
    {
        return !string.IsNullOrWhiteSpace(targetScene)
            && Application.CanStreamedLevelBeLoaded(targetScene);
    }

    private static TransitionDirection ResolveDirection(string currentScene, string targetScene)
    {
        EnsureCurrentInHistory(currentScene);

        int targetIndex = FindLastHistoryIndex(targetScene);
        if (targetIndex >= 0 && targetIndex < navigationHistory.Count - 1)
            return TransitionDirection.Back;

        if (IsExplicitBackToHub(currentScene, targetScene))
            return TransitionDirection.Back;

        return TransitionDirection.Forward;
    }

    private static bool IsExplicitBackToHub(string currentScene, string targetScene)
    {
        if (!IsHub(targetScene))
            return false;

        return IsScene(currentScene, "Customization")
            || IsScene(currentScene, "Settings")
            || IsScene(currentScene, "EnergyStation");
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

        if (navigationHistory.Count == 0
            || !string.Equals(
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
        rootObjectBuffer.Clear();
        scene.GetRootGameObjects(rootObjectBuffer);

        foreach (GameObject rootObject in rootObjectBuffer)
        {
            canvasBuffer.Clear();
            rootObject.GetComponentsInChildren(true, canvasBuffer);

            foreach (Canvas canvas in canvasBuffer)
            {
                if (canvas == null || canvas.rootCanvas != canvas)
                    continue;

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

        GameObject rootObject = new(TransitionRootName, typeof(RectTransform));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.anchoredPosition = Vector2.zero;
        root.SetSiblingIndex(0);

        // O contêiner mantém o layout original de todos os elementos da cena.
        while (canvas.transform.childCount > 1)
            canvas.transform.GetChild(1).SetParent(root, false);

        return root;
    }

    private static void PrepareIncomingRoots(List<RectTransform> roots, float side)
    {
        foreach (RectTransform root in roots)
        {
            if (root == null)
                continue;

            root.DOKill();
            root.localScale = Vector3.one;
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
        rootObjectBuffer.Clear();
        scene.GetRootGameObjects(rootObjectBuffer);

        foreach (GameObject rootObject in rootObjectBuffer)
        {
            eventSystemBuffer.Clear();
            rootObject.GetComponentsInChildren(true, eventSystemBuffer);

            foreach (EventSystem eventSystem in eventSystemBuffer)
            {
                if (eventSystem != null)
                    eventSystem.enabled = enabled;
            }
        }
    }

    private static void SetAudioListenersEnabled(Scene scene, bool enabled)
    {
        rootObjectBuffer.Clear();
        scene.GetRootGameObjects(rootObjectBuffer);

        foreach (GameObject rootObject in rootObjectBuffer)
        {
            audioListenerBuffer.Clear();
            rootObject.GetComponentsInChildren(true, audioListenerBuffer);

            foreach (AudioListener listener in audioListenerBuffer)
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
            if (scene.IsValid() && scene.isLoaded
                && string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
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

    private static bool IsHub(string sceneName)
    {
        return string.Equals(sceneName, "Hub", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sceneName, "HUB", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsScene(string sceneName, string expectedName)
    {
        return string.Equals(sceneName, expectedName, StringComparison.OrdinalIgnoreCase);
    }
}
