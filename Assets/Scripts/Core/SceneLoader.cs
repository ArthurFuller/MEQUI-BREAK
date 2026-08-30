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
///
/// Transição global:
/// - Avanço: a tela atual sai pela esquerda e a próxima entra pela direita.
/// - Retorno: a tela atual sai pela direita e a anterior entra pela esquerda.
///
/// Boot para Login permanece imediato porque não existe uma tela anterior para animar.
/// </summary>
public sealed class SceneLoader : MonoBehaviour
{
    private const string TransitionRootName = "__SceneTransitionRoot";

    [Header("Transição de cena - Deslizamento")]
    [Tooltip("Escala usada durante o deslocamento. O valor 0,97 reduz a tela em 3%.")]
    [SerializeField, Range(0.90f, 1f)]
    private float transitionScale = 0.97f;

    [Tooltip("Tempo para a tela atual alcançar a escala de transição.")]
    [SerializeField, Min(0.01f)]
    private float scaleOutDuration = 0.14f;

    [Tooltip("Tempo do deslizamento horizontal entre as duas cenas.")]
    [SerializeField, Min(0.01f)]
    private float slideDuration = 0.45f;

    [Tooltip("Tempo para a nova tela retornar da escala de transição até 1.")]
    [SerializeField, Min(0.01f)]
    private float scaleInDuration = 0.22f;

    [Tooltip("Pausa após a redução da tela atual e antes do deslizamento.")]
    [SerializeField, Min(0f)]
    private float preSlidePauseDuration = 0.10f;

    [Tooltip("Pausa após a nova tela chegar ao centro e antes de voltar à escala total.")]
    [SerializeField, Min(0f)]
    private float postSlidePauseDuration = 0.03f;

    [Header("Transição de cena - Curvas")]
    [SerializeField]
    private Ease scaleOutEase = Ease.OutQuad;

    [SerializeField]
    private Ease slideEase = Ease.InOutCubic;

    [SerializeField]
    private Ease scaleInEase = Ease.OutCubic;

    private static bool isTransitioning;
    private static readonly List<string> navigationHistory = new();
    private static readonly List<GameObject> rootObjectBuffer = new();
    private static readonly List<Canvas> canvasBuffer = new();
    private static readonly List<EventSystem> eventSystemBuffer = new();
    private static readonly List<AudioListener> audioListenerBuffer = new();

    /// <summary>Indica se duas cenas estão sendo preparadas ou animadas.</summary>
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

        // Usa carregamento comum quando a cena não participa da transição aditiva.
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

        // Bloqueia novos cliques enquanto a próxima cena é preparada.
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

        // Pré-carrega até 90% e troca o AudioListener somente antes da ativação.
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

        // O EventSystem da nova cena permanece desativado até o fim do movimento.
        SetEventSystemsEnabled(incomingScene, false);

        List<RectTransform> incomingRoots = GetOrCreateTransitionRoots(incomingScene);
        Canvas.ForceUpdateCanvases();

        float incomingSide = direction == TransitionDirection.Forward ? 1f : -1f;
        float outgoingSide = -incomingSide;

        PrepareIncomingRoots(incomingRoots, incomingSide);

        Sequence transition = DOTween.Sequence()
            .SetUpdate(true)
            .SetAutoKill(true);

        // As fases permanecem separadas: redução, pausa, deslizamento e ampliação.
        float slideStartTime = scaleOutDuration + preSlidePauseDuration;
        float scaleInStartTime = slideStartTime + slideDuration + postSlidePauseDuration;
        bool animateScale = !Mathf.Approximately(transitionScale, 1f);

        foreach (RectTransform root in outgoingRoots)
        {
            if (root == null)
                continue;

            if (animateScale)
            {
                transition.Insert(
                    0f,
                    root.DOScale(transitionScale, scaleOutDuration)
                        .SetEase(scaleOutEase));
            }

            float width = GetRootWidth(root);
            transition.Insert(
                slideStartTime,
                root.DOAnchorPosX(outgoingSide * width, slideDuration)
                    .SetEase(slideEase));
        }

        // A nova tela só retorna à escala 1 depois de chegar ao centro.
        foreach (RectTransform root in incomingRoots)
        {
            if (root == null)
                continue;

            transition.Insert(
                slideStartTime,
                root.DOAnchorPosX(0f, slideDuration)
                    .SetEase(slideEase));

            if (animateScale)
            {
                transition.Insert(
                    scaleInStartTime,
                    root.DOScale(1f, scaleInDuration)
                        .SetEase(scaleInEase));
            }
        }

        // Preserva a duração total mesmo quando a escala configurada é 1.
        transition.InsertCallback(scaleInStartTime + scaleInDuration, NoOp);

        yield return transition.WaitForCompletion();

        ResetRoots(incomingRoots);
        SceneManager.SetActiveScene(incomingScene);

        SetEventSystemsEnabled(incomingScene, true);

        CommitNavigation(currentScene.name, targetSceneName, direction);
        isTransitioning = false;

        // A partir daqui, a instância pertence somente à cena que será descarregada.
        SceneManager.UnloadSceneAsync(currentScene);
    }

    private static bool ShouldUseSlideTransition(string currentScene, string targetScene)
    {
        if (string.IsNullOrWhiteSpace(currentScene) || string.IsNullOrWhiteSpace(targetScene))
            return false;

        if (string.Equals(currentScene, targetScene, StringComparison.OrdinalIgnoreCase))
            return false;

        // Apenas cenas do Build Settings podem ser pré-carregadas de forma aditiva.
        return Application.CanStreamedLevelBeLoaded(targetScene);
    }

    private static TransitionDirection ResolveDirection(string currentScene, string targetScene)
    {
        EnsureCurrentInHistory(currentScene);

        // Uma cena anterior no histórico caracteriza navegação de retorno.
        int targetIndex = FindLastHistoryIndex(targetScene);
        if (targetIndex >= 0 && targetIndex < navigationHistory.Count - 1)
            return TransitionDirection.Back;

        // O retorno explícito ao HUB mantém a direção correta mesmo sem histórico.
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
        rootObjectBuffer.Clear();
        scene.GetRootGameObjects(rootObjectBuffer);

        foreach (GameObject rootObject in rootObjectBuffer)
        {
            canvasBuffer.Clear();
            rootObject.GetComponentsInChildren(true, canvasBuffer);
            foreach (Canvas canvas in canvasBuffer)
            {
                // Canvas aninhado acompanha automaticamente seu Canvas raiz.
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
        root.localScale = Vector3.one;
        root.anchoredPosition = Vector2.zero;
        root.SetSiblingIndex(0);

        // O contêiner possui o mesmo retângulo do Canvas e preserva o layout local.
        while (canvas.transform.childCount > 1)
            canvas.transform.GetChild(1).SetParent(root, false);

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

    private static void NoOp()
    {
    }
}
