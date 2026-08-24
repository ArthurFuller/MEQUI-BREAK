using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;
using System;

/// <summary>
/// Animates flying point coins from a spawn position to a target UI label.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class PointAnimationManager : MonoBehaviour
{
    public static PointAnimationManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Prefab to instantiate for each point (should be a UI Image under a Canvas)")]
    [SerializeField] private GameObject coinPrefab;

    [Tooltip("Target TMP_Text that displays the total points (e.g., Break Points label)")]
    [SerializeField] private TMP_Text pointsLabel;

    /// <summary>Read-only access to the points label for external setup (e.g., HubEntryHandler).</summary>
    public TMP_Text PointsLabel => pointsLabel;

    [Tooltip("RectTransform of the Canvas where coins will be spawned (usually the Canvas itself)")]
    [SerializeField] private RectTransform canvasRect;

    [Header("Animation Settings")]
    [Tooltip("Duration of each coin's flight")]
    [SerializeField, Min(0.1f)] private float duration = 0.8f;

    [Tooltip("Delay between each coin spawn (creates a stream effect)")]
    [SerializeField, Min(0f)] private float spawnDelay = 0.05f;

    [Tooltip("Vertical offset for the arc midpoint (higher = higher arc)")]
    [SerializeField] private Vector2 arcOffset = new Vector2(0f, 150f);

    // How many coins have actually landed so far in the current batch.
    // Used to derive the displayed number deterministically (baseValue + landed)
    // instead of parsing whatever text happens to be on the label.
    private int _coinsLanded;

    /// <summary>
    /// True while an animation is in progress. Allows HubController to defer
    /// its own Refresh() so it doesn't overwrite the animating value.
    /// </summary>
    public bool IsAnimating { get; private set; }

    /// <summary>
    /// Invoked when the current animation batch finishes (all coins landed).
    /// Used by HubEntryHandler to clear PendingBreakPoints only after the visual
    /// animation completes, preventing a race condition with HubController.Refresh().
    /// </summary>
    public event Action OnAnimationComplete;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Validate references
        if (coinPrefab == null)
            Debug.LogError("[PointAnimationManager] CoinPrefab is not assigned.", this);
        if (pointsLabel == null)
            Debug.LogError("[PointAnimationManager] PointsLabel TMP_Text is not assigned.", this);
        if (canvasRect == null)
            Debug.LogError("[PointAnimationManager] CanvasRect RectTransform is not assigned.", this);
    }

    /// <summary>
    /// Starts the animation for the given amount of points.
    /// </summary>
    /// <param name="baseValue">
    /// The player's real balance BEFORE this batch of points was earned.
    /// The label is snapped to this value immediately, so the coins always
    /// count up from the true starting point instead of from whatever was
    /// already on screen (which may already include the new points).
    /// </param>
    /// <param name="amount">Number of points to animate (each point spawns one coin)</param>
    public void AnimatePoints(int baseValue, int amount)
    {
        if (amount <= 0)
            return;

        // Clear any ongoing coroutines to avoid overlap
        StopAllCoroutines();
        _coinsLanded = 0;

        // Snap the label to the pre-earn baseline right now. This is what
        // guarantees correctness regardless of whether HubController.Refresh()
        // (which shows the already-updated, final value) ran before or after
        // this call.
        if (pointsLabel != null)
            pointsLabel.text = $"{baseValue} PB";

        StartCoroutine(SpawnCoinsRoutine(baseValue, amount));
    }

    private IEnumerator SpawnCoinsRoutine(int baseValue, int amount)
    {
        IsAnimating = true;
        try
        {
            for (int i = 0; i < amount; i++)
            {
                StartCoroutine(SpawnSingleCoin(baseValue));
                yield return new WaitForSeconds(spawnDelay);
            }

            // Wait for all coins to land (coinsLanded == amount)
            while (_coinsLanded < amount)
            {
                yield return null;
            }
        }
        finally
        {
            IsAnimating = false;
            OnAnimationComplete?.Invoke();
        }
    }

    private IEnumerator SpawnSingleCoin(int baseValue)
    {
        // Small random offset to avoid perfect overlap
        Vector2 spawnPos = (Vector2)canvasRect.position + UnityEngine.Random.insideUnitCircle * 10f;
        Vector2 targetPos = pointsLabel.rectTransform.position;

        // Calculate arc midpoint: average of start and end plus offset
        Vector2 midPoint = (spawnPos + targetPos) * 0.5f + arcOffset;

        // Instantiate coin prefab under the canvas
        GameObject coin = Instantiate(coinPrefab, canvasRect);
        coin.transform.position = spawnPos;

        // Define path for DOTween (CatmullRom spline through midpoint)
        Vector3[] path = new Vector3[] { midPoint, targetPos };

        // Create animation sequence
        Sequence seq = DOTween.Sequence();

        // Flight path
        seq.Append(coin.transform.DOPath(path, duration, PathType.CatmullRom).SetEase(Ease.OutQuad));

        // Rotation while flying
        seq.Join(coin.transform.DORotate(new Vector3(0f, 0f, 360f), duration, RotateMode.FastBeyond360));

        // Scale bounce (pop)
        seq.Join(coin.transform.DOScale(1.3f, duration * 0.3f).SetLoops(2, LoopType.Yoyo));

        // On complete: update label and destroy coin
        seq.OnComplete(() =>
        {
            // Each coin that lands increments a counter, and the label is
            // recomputed as baseValue + coins landed so far. This always ends
            // up exactly at baseValue + amount (the real, saved balance),
            // regardless of landing order or whatever text was on the label
            // before — no more parsing/guessing the "current" number.
            _coinsLanded++;

            if (pointsLabel != null)
            {
                // Kill any ongoing scale tween to avoid accumulation/residual scale
                pointsLabel.transform.DOKill();
                pointsLabel.transform.localScale = Vector3.one;
                pointsLabel.transform.DOPunchScale(Vector3.one * 0.1f, 0.1f);

                pointsLabel.text = $"{baseValue + _coinsLanded} PB";
            }

            // Cleanup
            if (coin != null)
                Destroy(coin);
        });

        // Wait for the sequence to finish before allowing next coin (optional)
        // Since we spawn with delay, we don't need to wait here.
        yield return seq.WaitForCompletion();
    }
}