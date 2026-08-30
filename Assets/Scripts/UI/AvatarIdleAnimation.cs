using UnityEngine;

/// <summary>
/// Aplica um movimento ocioso ao avatar em camadas sem animar seus sprites.
/// Os elementos filhos da cabeça acompanham o movimento automaticamente.
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
        float wave = Mathf.Sin(phase);

        if (avatarRoot != null)
        {
            float verticalOffset = wave * bobDistance;
            avatarRoot.anchoredPosition = initialRootPosition + Vector2.up * verticalOffset;
        }

        if (headAnchor != null)
        {
            float tilt = wave * headTiltDegrees;
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
