using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class AppBootstrapper : MonoBehaviour
{
    [Header("Serviços globais")]
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private SettingsManager settingsManager;

    [Header("Cenas iniciais")]
    [FormerlySerializedAs("firstScene")]
    [SerializeField] private string loginScene = "Login";
    [SerializeField] private string hubScene = "HUB";

    [Header("Fluxo inicial")]
    [Tooltip("Use apenas para testes. Em produção, o Login aparece somente sem cadastro válido salvo.")]
    [SerializeField] private bool alwaysShowLoginOnBoot;

    [Header("Tela de carregamento")]
    [SerializeField] private CanvasGroup loadingCanvas;
    [SerializeField] private RectTransform loadingOrbit;
    [SerializeField] private Graphic[] loadingTintTargets;
    [SerializeField, Min(0f)] private float minimumLoadingTime = 3f;

    private Tween orbitTween;
    private Tween tintTween;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        DontDestroyOnLoad(transform.root.gameObject);

        if (GetComponent<FirstRunGuideController>() == null)
            gameObject.AddComponent<FirstRunGuideController>();
    }

    private IEnumerator Start()
    {
        if (playerManager != null)
            playerManager.Initialize();

        if (settingsManager != null)
            settingsManager.Apply();

        bool hasRegistration = playerManager != null
            && playerManager.HasValidRegistration;

        // No Editor começa pelo Login; na build respeita o cadastro salvo.
        string targetScene = Application.isEditor
            || alwaysShowLoginOnBoot
            || !hasRegistration
            ? loginScene
            : hubScene;

        if (string.IsNullOrWhiteSpace(targetScene))
            yield break;

        StartLoadingAnimation();

        AsyncOperation loading = SceneManager.LoadSceneAsync(targetScene);
        if (loading == null)
            yield break;

        loading.allowSceneActivation = false;
        float elapsed = 0f;

        while (elapsed < minimumLoadingTime || loading.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (loadingCanvas != null)
            yield return loadingCanvas.DOFade(0f, 0.22f).SetUpdate(true).WaitForCompletion();

        StopLoadingAnimation();
        loading.allowSceneActivation = true;
    }

    private void StartLoadingAnimation()
    {
        if (loadingCanvas != null)
            loadingCanvas.alpha = 1f;

        if (loadingOrbit != null)
            orbitTween = loadingOrbit
                .DORotate(new Vector3(0f, 0f, -360f), 4.16f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1)
                .SetUpdate(true);

        if (loadingTintTargets == null || loadingTintTargets.Length == 0)
            return;

        Color tint = new(0.79f, 0.21f, 0.16f);
        ApplyTint(tint);

        Color[] cycle =
        {
            new(0.98f, 0.70f, 0.02f),
            new(0.29f, 0.58f, 0.43f),
            new(0.13f, 0.25f, 0.31f),
            new(0.79f, 0.21f, 0.16f)
        };

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        foreach (Color color in cycle)
        {
            Color target = color;
            sequence.Append(DOTween.To(() => tint, value =>
            {
                tint = value;
                ApplyTint(value);
            }, target, 0.78f).SetEase(Ease.InOutSine));
            sequence.AppendInterval(0.26f);
        }

        tintTween = sequence.SetLoops(-1);
    }

    private void ApplyTint(Color tint)
    {
        foreach (Graphic graphic in loadingTintTargets)
        {
            if (graphic == null)
                continue;

            float alpha = graphic.color.a;
            graphic.color = new Color(tint.r, tint.g, tint.b, alpha);
        }
    }

    private void StopLoadingAnimation()
    {
        orbitTween?.Kill();
        tintTween?.Kill();
        orbitTween = tintTween = null;
    }
}
