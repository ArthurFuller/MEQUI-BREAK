using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to each swatch/thumbnail button inside a customization options grid
/// (HairOptions, OutfitOptions or AccessoryOptions). It calls
/// CustomizationController.SelectOption(optionIndex) automatically, so you
/// don't need to configure a persistent int argument by hand on every button
/// in the Inspector — just set optionIndex and, optionally, drag the
/// CustomizationController reference.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class AvatarOptionButton : MonoBehaviour
{
    [SerializeField] private CustomizationController controller;
    [SerializeField, Min(0)] private int optionIndex;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (controller == null)
            controller = FindFirstObjectByType<CustomizationController>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        controller?.SelectOption(optionIndex);
    }
}
