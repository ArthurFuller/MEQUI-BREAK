using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a lightweight scale reaction that can be combined with AvatarIdleAnimation.
/// </summary>
public sealed class AvatarReactionAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform reactionTarget;
    [SerializeField, Min(1f)] private float peakScale = 1.1f;
    [SerializeField, Min(0.01f)] private float duration = 0.22f;

    private Vector3 initialScale;
    private Coroutine activeReaction;

    private void Awake()
    {
        if (reactionTarget == null)
            reactionTarget = transform as RectTransform;

        if (reactionTarget != null)
            initialScale = reactionTarget.localScale;
    }

    public void Play()
    {
        if (reactionTarget == null)
            return;

        if (activeReaction != null)
            StopCoroutine(activeReaction);

        activeReaction = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        float elapsed = 0f;
        Vector3 peak = initialScale * peakScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float scaleProgress = progress <= 0.5f
                ? progress / 0.5f
                : (1f - progress) / 0.5f;

            reactionTarget.localScale = Vector3.Lerp(initialScale, peak, scaleProgress);
            yield return null;
        }

        reactionTarget.localScale = initialScale;
        activeReaction = null;
    }

    private void OnDisable()
    {
        if (reactionTarget != null)
            reactionTarget.localScale = initialScale;
    }
}
