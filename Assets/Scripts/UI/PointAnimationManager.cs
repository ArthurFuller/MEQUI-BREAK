using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Animates flying point coins from a spawn position to a target UI label.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class PointAnimationManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Prefab to instantiate for each point (should be a UI Image under a Canvas)")]
    [SerializeField] private GameObject coinPrefab;

    [Tooltip("Target TMP_Text that displays the total points (e.g., Break Points label)")]
    [SerializeField] private TMP_Text pointsLabel;

    [Tooltip("RectTransform of the Canvas where coins will be spawned (usually the Canvas itself)")]
    [SerializeField] private RectTransform canvasRect;

    [Header("Animation Settings")]
    [Tooltip("Duration of each coin's flight")]
    [SerializeField, Min(0.1f)] private float duration = 0.8f;

    [Tooltip("Delay between each coin spawn (creates a stream effect)")]
    [SerializeField, Min(0f)] private float spawnDelay = 0.05f;

    [Tooltip("Vertical offset for the arc midpoint (higher = higher arc)")]
    [SerializeField] private Vector2 arcOffset = new Vector2(0f, 150f);

    private void Awake()
    {
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
    /// <param name="amount">Number of points to animate (each point spawns one coin)</param>
    public void AnimatePoints(int amount)
    {
        if (amount <= 0)
            return;

        // Clear any ongoing coroutines to avoid overlap (optional)
        StopAllCoroutines();
        StartCoroutine(SpawnCoinsRoutine(amount));
    }

    private IEnumerator SpawnCoinsRoutine(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            StartCoroutine(SpawnSingleCoin(i));
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private IEnumerator SpawnSingleCoin(int index)
    {
        // Small random offset to avoid perfect overlap
        Vector2 spawnPos = (Vector2)canvasRect.position + Random.insideUnitCircle * 10f;
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
            // Increment the displayed points by 1 (visual effect)
            // We rely on HubController to have updated the actual value already.
            // For a smooth visual increment, we animate the number and punch the label.
            if (pointsLabel != null)
            {
                // Kill any ongoing scale tween to avoid accumulation/residual scale
                pointsLabel.transform.DOKill();
                pointsLabel.transform.localScale = Vector3.one;
                pointsLabel.transform.DOPunchScale(Vector3.one * 0.1f, 0.1f);

                // Parse current displayed value and increment by 1
                string currentText = pointsLabel.text;
                int currentValue = 0;
                // Try to extract number from text like "120 PB" or just "120"
                var digits = System.Text.RegularExpressions.Regex.Match(currentText, @"\d+");
                if (digits.Success)
                {
                    currentValue = int.Parse(digits.Value);
                }
                int newValue = currentValue + 1;
                // Preserve any suffix (like " PB") from original text
                string suffix = currentText.Substring(digits.Index + digits.Length);
                pointsLabel.text = $"{newValue}{suffix}";
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