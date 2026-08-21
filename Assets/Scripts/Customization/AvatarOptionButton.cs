using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to each swatch/thumbnail button inside a customization options grid
/// (HairOptions, OutfitOptions or AccessoryOptions). It routes clicks through
/// CustomizationController.HandleOptionClicked(optionIndex), which checks the
/// catalog before applying the selection - locked items open a purchase
/// confirmation or a "needs Level X" message instead of being applied directly.
/// You don't need to configure a persistent int argument by hand on every
/// button in the Inspector - just set optionIndex and, optionally, drag the
/// CustomizationController reference and the lock visuals.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class AvatarOptionButton : MonoBehaviour
{
    [SerializeField] private CustomizationController controller;
    [SerializeField, Min(0)] private int optionIndex;

    [Header("Lock visuals (optional)")]
    [Tooltip("Shown/hidden by the controller when this item is locked/unlocked. Leave empty if you don't need a lock overlay yet.")]
    [SerializeField] private GameObject lockOverlay;
    [Tooltip("Shows the price (e.g. '40 PB') or requirement (e.g. 'Nv. 3') while locked.")]
    [SerializeField] private TMP_Text unlockLabel;

    private Button button;

    public int OptionIndex => optionIndex;

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
        controller?.HandleOptionClicked(optionIndex);
    }

    /// <summary>
    /// Called by CustomizationController after Start() and after any purchase to
    /// reflect the current unlock state. Safe to call even if the lock visuals
    /// were never assigned - it just skips them.
    /// </summary>
    public void SetLocked(bool isLocked, string label)
    {
        if (lockOverlay != null)
            lockOverlay.SetActive(isLocked);

        if (unlockLabel != null)
        {
            unlockLabel.gameObject.SetActive(isLocked && !string.IsNullOrEmpty(label));
            unlockLabel.text = label;
        }
    }
}
