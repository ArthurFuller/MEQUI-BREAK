using UnityEngine;

/// <summary>
/// Ajusta um contêiner de interface à área segura da tela atual.
/// Fundos que cobrem a tela inteira devem ficar fora deste objeto.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    [SerializeField] private bool applyHorizontalInsets = true;
    [SerializeField] private bool applyVerticalInsets = true;

    private RectTransform _rectTransform;
    private Rect _lastSafeArea = new(-1f, -1f, -1f, -1f);
    private Vector2Int _lastScreenSize = new(-1, -1);
    private bool _isApplying;

    private void OnEnable()
    {
        CacheRectTransform();
        ApplySafeArea(force: true);
    }

    private void Start()
    {
        ApplySafeArea(force: true);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled || _isApplying)
            return;

        CacheRectTransform();
        ApplySafeArea(force: false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ApplySafeArea(force: true);
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (!isPaused)
            ApplySafeArea(force: true);
    }

#if UNITY_EDITOR
    private void Update()
    {
        // No Editor, o Device Simulator pode alterar a área segura sem callback de dimensão.
        if (!Application.isPlaying)
            ApplySafeArea(force: false);
    }
#endif

    /// <summary>Força uma atualização imediata para testes e ferramentas do Editor.</summary>
    public void Refresh()
    {
        ApplySafeArea(force: true);
    }

    private void CacheRectTransform()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
    }

    private void ApplySafeArea(bool force)
    {
        CacheRectTransform();

        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        if (_rectTransform == null || screenWidth <= 0 || screenHeight <= 0)
            return;

        Rect safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
            safeArea = new Rect(0f, 0f, screenWidth, screenHeight);

        if (!applyHorizontalInsets)
        {
            safeArea.x = 0f;
            safeArea.width = screenWidth;
        }

        if (!applyVerticalInsets)
        {
            safeArea.y = 0f;
            safeArea.height = screenHeight;
        }

        Vector2Int screenSize = new(screenWidth, screenHeight);
        if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize)
            return;

        Vector2 anchorMin = new(
            safeArea.xMin / screenWidth,
            safeArea.yMin / screenHeight);

        Vector2 anchorMax = new(
            safeArea.xMax / screenWidth,
            safeArea.yMax / screenHeight);

        _isApplying = true;
        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
        _isApplying = false;

        _lastSafeArea = safeArea;
        _lastScreenSize = screenSize;
    }
}
