using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controla somente o fluxo do tutorial. Não calcula posição, tamanho, âncora
/// ou recorte: todo o layout permanece exatamente como foi montado na cena.
/// </summary>
public sealed class FirstRunGuideController : MonoBehaviour
{
    private const int HubStep = 0;
    private const int EnergyStationStep = 2;
    private const int CompletedStep = 3;
    private const int TotalSteps = 2;

    private const string HubSceneName = "HUB";
    private const string EnergyStationSceneName = "EnergyStation";

    [Header("Busca da lógica")]
    [Tooltip("Tempo máximo para aguardar as referências da cena após uma transição.")]
    [SerializeField, Min(0.5f)] private float targetSearchTimeout = 5f;

    private FirstRunGuideSceneView currentView;
    private Button targetButton;
    private EnergyStationController energyStation;
    private DraggableInteraction energyGuideCard;
    private Coroutine searchRoutine;
    private int energyGuideHighlightIndex;
    private int showingStep = -1;
    private bool showing;

    private void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        QueueScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        StopSearch();
        HideGuide();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (!IsGuideScene(scene.name))
        {
            HideGuide();
            return;
        }

        QueueScene(scene);
    }

    private void QueueScene(Scene scene)
    {
        StopSearch();
        HideGuide();

        if (scene.IsValid() && scene.isLoaded && IsGuideScene(scene.name))
        {
            // Os destaques ficam ativos na Hierarchy para edição manual, mas
            // precisam começar ocultos caso o guia já tenha sido concluído.
            FindSceneComponent<FirstRunGuideSceneView>(scene)?.Hide();
            searchRoutine = StartCoroutine(WaitForManualView(scene));
        }
    }

    private IEnumerator WaitForManualView(Scene scene)
    {
        yield return new WaitForEndOfFrame();

        float elapsed = 0f;
        while (scene.IsValid() && scene.isLoaded && elapsed < targetSearchTimeout)
        {
            if (SceneLoader.IsTransitionInProgress
                || SceneManager.GetActiveScene() != scene)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return new WaitForEndOfFrame();
                continue;
            }

            FirstRunGuideSceneView view = FindSceneComponent<FirstRunGuideSceneView>(scene);
            int requestedStep = ResolveRequestedStep(scene, view);
            if (view != null
                && requestedStep >= 0
                && !view.SupportsStep(requestedStep))
            {
                break;
            }

            if (view != null && view.IsReadyForStep(requestedStep))
            {
                if (!CanShow(view, requestedStep))
                    break;

                Button button = ResolveTargetButton(view, requestedStep);
                EnergyStationController station = ResolveEnergyStation(scene, view, requestedStep);
                bool logicReady = requestedStep == EnergyStationStep
                    ? station != null
                    : button != null;

                if (logicReady)
                {
                    searchRoutine = null;
                    ShowGuide(view, button, station, requestedStep);
                    yield break;
                }
            }

            elapsed += Time.unscaledDeltaTime;
            yield return new WaitForEndOfFrame();
        }

        searchRoutine = null;
    }

    private bool CanShow(FirstRunGuideSceneView view, int requestedStep)
    {
        PlayerManager player = PlayerManager.Instance;
        PlayerProfileData profile = player?.Profile;
        if (player == null || profile == null || !player.HasValidRegistration)
            return false;

        if (view.ExecutarSempreParaTeste)
            return view.SupportsStep(requestedStep);

        bool isCompatibleHubStep = requestedStep == HubStep
            && (profile.OnboardingStep == HubStep || profile.OnboardingStep == 1);
        bool isCompatibleEnergyStep = requestedStep == EnergyStationStep
            && profile.OnboardingStep == 1;

        return profile.OnboardingStep < CompletedStep
            && (profile.OnboardingStep == requestedStep
                || isCompatibleHubStep
                || isCompatibleEnergyStep)
            && view.SupportsStep(requestedStep);
    }

    private void ShowGuide(
        FirstRunGuideSceneView view,
        Button button,
        EnergyStationController station,
        int requestedStep)
    {
        RemoveTargetListener();
        RemoveEnergyStationListeners();

        currentView = view;
        targetButton = button;
        energyStation = station;
        showingStep = requestedStep;
        showing = true;

        if (targetButton != null)
            targetButton.onClick.AddListener(HandleTargetClicked);

        if (currentView.SkipButton != null)
            currentView.SkipButton.onClick.AddListener(SkipGuide);

        if (energyStation != null)
        {
            energyStation.InteractionAccepted += HandleEnergyInteractionAccepted;
            energyStation.ChoicesReset += HandleEnergyChoicesReset;
            energyGuideHighlightIndex = 0;
            BindEnergyGuideCard(energyStation.GetFirstAvailableInteraction());
        }

        currentView.Show(
            showingStep,
            showingStep == EnergyStationStep ? 2 : 1,
            TotalSteps,
            GetStepMessage(showingStep));
    }

    private void HandleTargetClicked()
    {
        if (!showing)
            return;

        int completedStep = showingStep;
        bool testing = IsTestingMode();
        if (testing)
        {
            HideGuide();
            return;
        }

        HideGuide();

        PlayerManager player = PlayerManager.Instance;
        PlayerProfileData profile = player?.Profile;
        if (profile == null)
            return;

        if (completedStep == HubStep
            && (profile.OnboardingStep == HubStep || profile.OnboardingStep == 1))
            profile.OnboardingStep = EnergyStationStep;
        else
            return;

        player.SaveProfile();
    }

    public void RestartGuide()
    {
        PlayerManager player = PlayerManager.Instance;
        if (SceneLoader.IsTransitionInProgress
            || player?.Profile == null
            || !player.HasValidRegistration)
            return;

        if (!IsTestingMode())
        {
            player.Profile.OnboardingStep = HubStep;
            player.SaveProfile();
        }
        HideGuide();
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == HubSceneName)
        {
            QueueScene(activeScene);
            return;
        }

        FindFirstObjectByType<SceneLoader>()?.Load(HubSceneName);
    }

    private void HandleEnergyInteractionAccepted(int acceptedCount)
    {
        if (!showing || showingStep != EnergyStationStep || energyStation == null)
            return;

        if (acceptedCount >= energyStation.InteractionsToComplete)
        {
            UnbindEnergyGuideCard();
            StopSearch();
            searchRoutine = StartCoroutine(CompleteEnergyGuideAfterDrop());
            return;
        }

        energyGuideHighlightIndex = acceptedCount;
        UnbindEnergyGuideCard();
        currentView?.ShowEnergyHighlight(energyGuideHighlightIndex);
        StartCoroutine(BindNextEnergyGuideCardAfterDrop());
        currentView?.SetMessage("Muito bem! Agora arraste mais um card até o avatar.");
    }

    private IEnumerator BindNextEnergyGuideCardAfterDrop()
    {
        yield return null;

        if (showing && showingStep == EnergyStationStep && energyStation != null)
            BindEnergyGuideCard(energyStation.GetFirstAvailableInteraction());
    }

    private IEnumerator CompleteEnergyGuideAfterDrop()
    {
        yield return null;
        searchRoutine = null;

        if (showing && showingStep == EnergyStationStep)
            CompleteEnergyGuide();
    }

    private void HandleEnergyChoicesReset()
    {
        if (showing && showingStep == EnergyStationStep)
        {
            energyGuideHighlightIndex = 0;
            BindEnergyGuideCard(energyStation?.GetFirstAvailableInteraction());
            currentView?.ShowEnergyHighlight(0);
            currentView?.SetMessage("Escolha um card e arraste-o até o avatar.");
        }
    }

    private void CompleteEnergyGuide()
    {
        PlayerManager player = PlayerManager.Instance;
        bool testing = IsTestingMode();
        bool canComplete = testing
            || (player?.Profile != null
                && (player.Profile.OnboardingStep == EnergyStationStep
                    || player.Profile.OnboardingStep == 1));

        HideGuide();
        if (!canComplete || testing)
            return;

        player.Profile.OnboardingStep = CompletedStep;
        player.SaveProfile();
    }

    private void SkipGuide()
    {
        bool testing = IsTestingMode();
        if (!testing)
        {
            PlayerManager player = PlayerManager.Instance;
            if (player?.Profile != null)
            {
                player.Profile.OnboardingStep = CompletedStep;
                player.SaveProfile();
            }
        }

        HideGuide();
    }

    private void HideGuide()
    {
        RemoveTargetListener();
        RemoveEnergyStationListeners();

        if (currentView != null)
        {
            if (currentView.SkipButton != null)
                currentView.SkipButton.onClick.RemoveListener(SkipGuide);
            currentView.Hide();
        }

        currentView = null;
        showing = false;
        showingStep = -1;
    }

    private void RemoveTargetListener()
    {
        if (targetButton != null)
            targetButton.onClick.RemoveListener(HandleTargetClicked);
        targetButton = null;
    }

    private void RemoveEnergyStationListeners()
    {
        UnbindEnergyGuideCard();
        energyGuideHighlightIndex = 0;

        if (energyStation != null)
        {
            energyStation.InteractionAccepted -= HandleEnergyInteractionAccepted;
            energyStation.ChoicesReset -= HandleEnergyChoicesReset;
        }
        energyStation = null;
    }

    private void BindEnergyGuideCard(DraggableInteraction interaction)
    {
        if (energyGuideCard == interaction)
            return;

        UnbindEnergyGuideCard();
        energyGuideCard = interaction;
        if (energyGuideCard == null)
            return;

        energyGuideCard.DragStarted += HandleEnergyGuideDragStarted;
        energyGuideCard.ReturnedToOrigin += HandleEnergyGuideReturnedToOrigin;
    }

    private void UnbindEnergyGuideCard()
    {
        if (energyGuideCard != null)
        {
            energyGuideCard.DragStarted -= HandleEnergyGuideDragStarted;
            energyGuideCard.ReturnedToOrigin -= HandleEnergyGuideReturnedToOrigin;
        }

        energyGuideCard = null;
    }

    private void HandleEnergyGuideDragStarted(DraggableInteraction interaction)
    {
        if (showing
            && showingStep == EnergyStationStep
            && interaction == energyGuideCard)
            currentView?.HideEnergyHighlights();
    }

    private void HandleEnergyGuideReturnedToOrigin(DraggableInteraction interaction)
    {
        if (showing
            && showingStep == EnergyStationStep
            && interaction == energyGuideCard)
            currentView?.ShowEnergyHighlight(energyGuideHighlightIndex);
    }

    private void StopSearch()
    {
        if (searchRoutine != null)
            StopCoroutine(searchRoutine);
        searchRoutine = null;
    }

    private bool IsTestingMode()
    {
        return currentView != null && currentView.ExecutarSempreParaTeste;
    }

    private int ResolveRequestedStep(Scene scene, FirstRunGuideSceneView view)
    {
        if (view == null)
            return -1;

        if (view.ExecutarSempreParaTeste)
            return scene.name == HubSceneName ? HubStep : EnergyStationStep;

        int savedStep = PlayerManager.Instance?.Profile?.OnboardingStep ?? -1;
        if (scene.name == HubSceneName && savedStep == 1)
            return HubStep;
        if (scene.name == EnergyStationSceneName && savedStep == 1)
            return EnergyStationStep;

        return savedStep;
    }

    private static Button ResolveTargetButton(FirstRunGuideSceneView view, int requestedStep)
    {
        return view != null ? view.GetTargetButton(requestedStep) : null;
    }

    private static EnergyStationController ResolveEnergyStation(
        Scene scene,
        FirstRunGuideSceneView view,
        int requestedStep)
    {
        if (requestedStep != EnergyStationStep)
            return null;

        return view.EnergyStation != null
            ? view.EnergyStation
            : FindSceneComponent<EnergyStationController>(scene);
    }

    private static string GetStepMessage(int step)
    {
        return step switch
        {
            HubStep => "Toque em Energy Station para começar.",
            _ => "Arraste um card até o avatar para indicar uma pausa."
        };
    }

    private static bool IsGuideScene(string sceneName)
    {
        return sceneName == HubSceneName
            || sceneName == EnergyStationSceneName;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }
}
