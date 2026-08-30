using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Exibe os estados normal e de sucesso do mascote após um drop válido.</summary>
[DisallowMultipleComponent]
public sealed class MequiEnergyArtFeedback : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite successSprite;
    [SerializeField, Min(0.1f)] private float successDuration = 0.65f;

    private Sequence sequence;

    private void Awake()
    {
        if (targetImage != null)
        {
            targetImage.sprite = normalSprite;
            targetImage.preserveAspect = true;
        }
    }

    private void OnDestroy()
    {
        KillSequence();
    }

    private void OnDisable()
    {
        KillSequence();

        if (targetImage != null)
        {
            targetImage.sprite = normalSprite;
            targetImage.rectTransform.localScale = Vector3.one;
        }
    }

    public void PlaySuccess()
    {
        if (targetImage == null)
            return;

        KillSequence();

        targetImage.sprite = successSprite != null ? successSprite : normalSprite;
        targetImage.rectTransform.localScale = Vector3.one;

        sequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(targetImage.rectTransform.DOPunchScale(Vector3.one * 0.12f, 0.32f, 6, 0.5f))
            .AppendInterval(successDuration)
            .AppendCallback(() =>
            {
                if (targetImage != null)
                    targetImage.sprite = normalSprite;

                sequence = null;
            });
    }

    private void KillSequence()
    {
        if (sequence != null && sequence.IsActive())
            sequence.Kill();

        sequence = null;
    }
}
