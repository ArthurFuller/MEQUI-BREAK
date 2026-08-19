using UnityEngine;

/// <summary>
/// Gives a layered UI avatar a reusable idle motion without animating its sprites.
/// Children of the head anchor, such as hair and hats, follow its motion automatically.
/// </summary>
public sealed class AvatarIdleAnimation : MonoBehaviour
{
    [SerializeField] private RectTransform avatarRoot;
    [SerializeField] private RectTransform headAnchor;
    [SerializeField, Min(0f)] private float bobDistance = 10f;
    [SerializeField, Min(0f)] private float headTiltDegrees = 4f;
    [SerializeField, Min(0.01f)] private float cycleDuration = 1.35f;

    private Vector2 initialRootPosition;
    private Quaternion initialHeadRotation;

    private void Awake()
    {
        if (avatarRoot == null)
            avatarRoot = transform as RectTransform;

        if (avatarRoot != null)
            initialRootPosition = avatarRoot.anchoredPosition;

        if (headAnchor != null)
            initialHeadRotation = headAnchor.localRotation;
    }

    private void Update()
    {
        float phase = Time.unscaledTime * Mathf.PI * 2f / cycleDuration;

        if (avatarRoot != null)
        {
            float verticalOffset = Mathf.Sin(phase) * bobDistance;
            avatarRoot.anchoredPosition = initialRootPosition + Vector2.up * verticalOffset;
        }

        if (headAnchor != null)
        {
            float tilt = Mathf.Sin(phase) * headTiltDegrees;
            headAnchor.localRotation = initialHeadRotation * Quaternion.Euler(0f, 0f, tilt);
        }
    }

    private void OnDisable()
    {
        if (avatarRoot != null)
            avatarRoot.anchoredPosition = initialRootPosition;

        if (headAnchor != null)
            headAnchor.localRotation = initialHeadRotation;
    }
}
