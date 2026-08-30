using UnityEngine;
using UnityEngine.UI;

public sealed class AvatarView : MonoBehaviour
{
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image hairImage;
    [SerializeField] private Image outfitImage;
    [SerializeField] private Image accessoryImage;
    [SerializeField] private Sprite[] bodyOptions;
    [SerializeField] private Sprite[] hairOptions;
    [SerializeField] private Sprite[] outfitOptions;
    [SerializeField] private Sprite[] accessoryOptions;
    [SerializeField] private Animator animator;

    public void Apply(AvatarCustomizationData data)
    {
        if (data == null)
            return;

        SetSprite(bodyImage, bodyOptions, data.BodyIndex);
        SetSprite(hairImage, hairOptions, data.HairIndex);
        SetSprite(outfitImage, outfitOptions, data.OutfitIndex);
        SetSprite(accessoryImage, accessoryOptions, data.AccessoryIndex);
    }

    public void PlayReaction(string triggerName)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
            animator.SetTrigger(triggerName);

    }

    private static void SetSprite(Image target, Sprite[] options, int index)
    {
        if (target == null || options == null || options.Length == 0)
            return;

        int safeIndex = Mathf.Clamp(index, 0, options.Length - 1);
        target.sprite = options[safeIndex];
        target.enabled = options[safeIndex] != null;
    }
}
