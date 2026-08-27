using DG.Tweening;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edits an avatar in memory and only persists the changes after confirmation.
/// Locked items (Break Points or Level) are checked against the catalog before
/// a selection is applied; PB items open a short confirmation, Level items just
/// explain what's needed.
///
/// Orchestrates the customization screen, option selection, unlock rules,
/// purchase confirmation and preview persistence.
/// </summary>
public sealed class CustomizationController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private string profileScene = "Profile";
    [SerializeField] private string hubScene = "HUB";

    [Header("Preview")]
    [SerializeField] private AvatarView avatarPreview;

    [Header("Catalog")]
    [Tooltip("Central unlock rules. If left empty, every option is treated as Free.")]
    [SerializeField] private AvatarCustomizationCatalog catalog;

    [Header("Controls")]
    [SerializeField] private Button hairButton;
    [SerializeField] private Button outfitButton;
    [SerializeField] private Button accessoryButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Options")]
    [SerializeField] private GameObject[] hairOptions;
    [SerializeField] private GameObject[] outfitOptions;
    [SerializeField] private GameObject[] accessoryOptions;

    [Header("Feedback (optional)")]
    [SerializeField] private TMP_Text feedbackLabel;

    [Header("Purchase confirmation (optional)")]
    [Tooltip("If left empty, a Break Points purchase is attempted immediately on click instead of asking for confirmation first.")]
    [SerializeField] private GameObject purchaseConfirmPanel;
    [SerializeField] private TMP_Text purchaseConfirmLabel;
    [SerializeField] private Button purchaseConfirmYesButton;
    [SerializeField] private Button purchaseConfirmNoButton;

    [Header("Option Grid Wave Animation")]
    [Tooltip("Plays a bottom-to-top wave when a customization category becomes visible.")]
    [SerializeField] private bool animateOptionWave = true;

    [Tooltip("Duration of each option's entrance movement.")]
    [SerializeField, Min(0.05f)] private float optionWaveDuration = 0.32f;

    [Tooltip("Delay between options inside the same row.")]
    [SerializeField, Min(0f)] private float optionWaveItemDelay = 0.045f;

    [Tooltip("Additional delay when the wave advances to the next row.")]
    [SerializeField, Min(0f)] private float optionWaveRowDelay = 0.08f;

    [Tooltip("How far below the final position each option starts.")]
    [SerializeField, Min(0f)] private float optionWaveStartOffset = 80f;

    [Tooltip("Ease used by the option entrance movement.")]
    [SerializeField] private Ease optionWaveEase = Ease.OutCubic;

    [Header("Purchase Panel Animation")]
    [Tooltip("Duration of purchase panel entrance")]
    [SerializeField, Min(0.05f)] private float purchasePanelEnterDuration = 0.25f;

    [Tooltip("Duration of purchase panel exit")]
    [SerializeField, Min(0.05f)] private float purchasePanelExitDuration = 0.2f;

    [Tooltip("Scale factor for purchase panel entrance")]
    [SerializeField, Min(1f)] private float purchasePanelEnterScale = 1.1f;

    private readonly AvatarCustomizationData previewData = new AvatarCustomizationData();
    private AvatarCustomizationCategory selectedCategory = AvatarCustomizationCategory.Hair;
    private AvatarCustomizationItem pendingPurchaseItem;

    // Cached once in Start() instead of calling GetComponent<AvatarOptionButton>()
    // on every option GameObject each time the lock overlays refresh.
    private AvatarOptionButton[] hairButtons;
    private AvatarOptionButton[] outfitButtons;
    private AvatarOptionButton[] accessoryButtons;

    // Cached RectTransform for purchase panel animation
    private RectTransform _purchasePanelRect;

    // Cached CanvasGroup for fade animation
    private CanvasGroup _purchasePanelCanvasGroup;

    // Single DOTween Sequence controlling the currently visible option wave.
    private Sequence _optionWaveSequence;
    private readonly List<OptionWaveTarget> _activeOptionWaveTargets = new List<OptionWaveTarget>();

    // Single tracked sequence for the temporary feedback label. Replacing it
    // prevents delayed fade-outs from older messages from fighting newer ones.
    private Sequence _feedbackSequence;

    private sealed class OptionWaveTarget
    {
        public RectTransform RectTransform;
        public CanvasGroup CanvasGroup;
        public Vector2 FinalPosition;
        public float X;
        public float Y;
    }

    private void Awake()
    {
        // Customization can be loaded additively while the previous scene is still
        // sliding away. Hide the option cards before the first rendered frame so
        // they never flash at their final state before the entrance wave begins.
        // This uses a CanvasGroup on the option root only; it does not rewrite the
        // Image alpha configured on children such as UnlockOverlay (0.8 in scene).
        PrepareOptionsHiddenForInitialWave(hairOptions);
        PrepareOptionsHiddenForInitialWave(outfitOptions);
        PrepareOptionsHiddenForInitialWave(accessoryOptions);
    }

    private void Start()
    {
        CopyFromSavedAvatar();
        ApplyPreview();
        CacheOptionButtons();
        BindButtons();
        RefreshVisibleOptions();
        RefreshLockVisuals();
        CachePanelReferences();
        StartCoroutine(PlayInitialOptionWaveWhenReady());

        // Ensure purchase panel starts hidden
        if (purchaseConfirmPanel != null)
            purchaseConfirmPanel.SetActive(false);

    }


    /// <summary>
    /// Places option roots in an invisible pre-wave state before the first frame.
    /// The objects remain active so GridLayoutGroup can calculate their final
    /// positions normally when PlayOptionWave starts.
    /// </summary>
    private void PrepareOptionsHiddenForInitialWave(GameObject[] options)
    {
        if (!animateOptionWave || options == null)
            return;

        for (int i = 0; i < options.Length; i++)
        {
            GameObject option = options[i];
            if (option == null)
                continue;

            CanvasGroup canvasGroup = option.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = option.AddComponent<CanvasGroup>();

            // Only the parent CanvasGroup is used as the wave visibility gate.
            // Child graphic alpha values remain exactly as authored in the scene.
            canvasGroup.alpha = 0f;
        }
    }


    /// <summary>
    /// When Customization is loaded as the incoming side of the global scene
    /// transition, wait until the slide/scale settles before starting the option
    /// wave. The wave itself is unchanged; this only prevents two entrance motions
    /// from playing on top of each other.
    /// </summary>
    private IEnumerator PlayInitialOptionWaveWhenReady()
    {
        while (SceneLoader.IsTransitionInProgress)
            yield return null;

        // Let the final layout settle for one frame after the transition root is
        // reset/unloaded before capturing option positions for the existing wave.
        yield return null;
        PlayOptionWave();
    }

    private void OnDisable()
    {
        // Restore any in-progress wave so disabling/re-enabling the screen
        // cannot leave options offset or transparent.
        KillOptionWave(true);
        KillFeedbackTween();
    }

    private void OnDestroy()
    {
        KillOptionWave(false);
        KillFeedbackTween();
        UnbindButtons();
    }

    /// <summary>
    /// Caches RectTransform and CanvasGroup for the purchase panel.
    /// </summary>
    private void CachePanelReferences()
    {
        if (purchaseConfirmPanel == null) return;

        _purchasePanelRect = purchaseConfirmPanel.GetComponent<RectTransform>();
        if (_purchasePanelRect == null)
            _purchasePanelRect = purchaseConfirmPanel.GetComponentInParent<RectTransform>();

        _purchasePanelCanvasGroup = purchaseConfirmPanel.GetComponent<CanvasGroup>();
        if (_purchasePanelCanvasGroup == null && _purchasePanelRect != null)
            _purchasePanelCanvasGroup = purchaseConfirmPanel.AddComponent<CanvasGroup>();
    }

    // ============================================================
    // TAB SWITCHING
    // ============================================================

    public void SelectHair()
    {
        selectedCategory = AvatarCustomizationCategory.Hair;
        RefreshVisibleOptions();
        PlayOptionWave();
    }

    public void SelectOutfit()
    {
        selectedCategory = AvatarCustomizationCategory.Outfit;
        RefreshVisibleOptions();
        PlayOptionWave();
    }

    public void SelectAccessory()
    {
        selectedCategory = AvatarCustomizationCategory.Accessory;
        RefreshVisibleOptions();
        PlayOptionWave();
    }

    // ============================================================
    // OPTION SELECTION
    // ============================================================

    /// <summary>
    /// Entry point for swatch buttons. Checks the catalog first: unlocked items
    /// get applied to the preview right away; locked Break Points items open a
    /// purchase confirmation; locked Level items just show what's required.
    /// </summary>
    public void HandleOptionClicked(int optionIndex)
    {
        if (optionIndex < 0)
            return;

        AvatarCustomizationItem item = catalog != null ? catalog.GetItem(selectedCategory, optionIndex) : null;

        if (IsItemUnlocked(item))
        {
            SelectOption(optionIndex);
            return;
        }

        HandleLockedOptionClicked(item, optionIndex);
    }

    /// <summary>
    /// Applies an option index to the in-memory preview without any unlock
    /// check. Called internally once an item is known to be unlocked (or was
    /// just purchased) — kept public in case a caller elsewhere needs to force
    /// a selection (e.g. tests).
    /// </summary>
    public void SelectOption(int optionIndex)
    {
        if (optionIndex < 0)
            return;

        switch (selectedCategory)
        {
            case AvatarCustomizationCategory.Hair:
                previewData.HairIndex = optionIndex;
                break;
            case AvatarCustomizationCategory.Outfit:
                previewData.OutfitIndex = optionIndex;
                break;
            case AvatarCustomizationCategory.Accessory:
                previewData.AccessoryIndex = optionIndex;
                break;
        }

        ApplyPreview();

        // Play selection confirmed animation on the clicked button
        AnimateSelectionConfirmed(optionIndex);

    }

    /// <summary>
    /// Animates the selection confirmed on a specific option button.
    /// </summary>
    private void AnimateSelectionConfirmed(int optionIndex)
    {
        AvatarOptionButton[] buttons = GetButtonsForCategory(selectedCategory);
        if (buttons == null) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].OptionIndex == optionIndex)
            {
                buttons[i].AnimateSelectionConfirmed();
                break;
            }
        }
    }

    private AvatarOptionButton[] GetButtonsForCategory(AvatarCustomizationCategory category)
    {
        return category switch
        {
            AvatarCustomizationCategory.Hair => hairButtons,
            AvatarCustomizationCategory.Outfit => outfitButtons,
            AvatarCustomizationCategory.Accessory => accessoryButtons,
            _ => null
        };
    }

    // ============================================================
    // CONFIRM / CANCEL
    // ============================================================

    public void Confirm()
    {
        PlayerManager player = PlayerManager.Instance;
        if (player == null)
        {
            Debug.LogError("CustomizationController.Confirm: PlayerManager.Instance is null. Avatar was not saved.");
            return;
        }

        // Be defensive when the Customization scene is opened directly during
        // development/testing or when loading an older profile.
        if (player.Profile == null)
            player.Initialize();

        if (player.Profile == null)
        {
            Debug.LogError("CustomizationController.Confirm: Player profile is unavailable. Avatar was not saved.");
            return;
        }

        player.Profile.Avatar ??= new AvatarCustomizationData();

        AvatarCustomizationData savedAvatar = player.Profile.Avatar;
        savedAvatar.BodyIndex = previewData.BodyIndex;
        savedAvatar.HairIndex = previewData.HairIndex;
        savedAvatar.OutfitIndex = previewData.OutfitIndex;
        savedAvatar.AccessoryIndex = previewData.AccessoryIndex;

        player.SaveProfile();
        AudioManager.Instance?.PlayConfirm();

        sceneLoader?.Load(profileScene);
    }

    public void Cancel()
    {
        // previewData was never written into PlayerManager.Instance.Profile.Avatar,
        // so simply leaving without calling Confirm() already discards every change
        // made this session and keeps whatever was saved before entering this scene.
        AudioManager.Instance?.PlayClick();

        sceneLoader?.Load(hubScene);
    }

    public void Back() => Cancel();



    // ============================================================
    // PURCHASE FLOW
    // ============================================================

    /// <summary>Confirms the pending Break Points purchase (wire to the Yes button).</summary>
    public void ConfirmPurchase()
    {
        // Keep a local reference until the transaction has completed. This is
        // important because the confirmation panel is animated out and the
        // pending item must never be lost before the purchase is processed.
        AvatarCustomizationItem item = pendingPurchaseItem;

        if (item == null)
        {
            ShowFeedback("Nenhum item de compra selecionado.");
            AnimatePurchasePanelExit();
            return;
        }

        PlayerManager player = PlayerManager.Instance;
        if (player == null)
        {
            ShowFeedback("Sistema de jogador indisponível.");
            AnimatePurchasePanelExit();
            return;
        }

        // Do not clear pendingPurchaseItem until the transaction has actually
        // succeeded. This makes repeated clicks and UI animation safe.
        if (item.UnlockType != AvatarUnlockType.BreakPoints)
        {
            pendingPurchaseItem = null;
            AnimatePurchasePanelExit();
            return;
        }

        if (!player.TrySpendBreakPoints(item.BreakPointCost))
        {
            // Purchase was rejected: no profile data is changed and the item
            // remains locked. This is the expected result for insufficient PB.
            ShowFeedback($"PB insuficiente. Necessário: {item.BreakPointCost} PB.");
            pendingPurchaseItem = null;
            AnimatePurchasePanelExit();
            RefreshLockVisuals();
            return;
        }

        // Transaction succeeded: unlock the catalog item and persist both the
        // new unlock and the reduced spendable PB balance.
        player.UnlockCustomization(item.Id);
        player.SaveProfile();
        pendingPurchaseItem = null;

        // Update the UI only after the transaction has succeeded.
        AnimatePurchasePanelExit(() =>
        {
            SelectOption(item.OptionIndex);
            RefreshLockVisuals();
            ShowFeedback($"{DisplayNameOrFallback(item)} desbloqueado!");
            AnimateUnlockedButton(item.OptionIndex);
        });
    }

    /// <summary>Cancels the pending Break Points purchase (wire to the No button).</summary>
    public void CancelPurchase()
    {
        pendingPurchaseItem = null;
        AnimatePurchasePanelExit();
    }

    /// <summary>
    /// Animates a newly unlocked button after purchase.
    /// </summary>
    private void AnimateUnlockedButton(int optionIndex)
    {
        AvatarOptionButton[] buttons = GetButtonsForCategory(selectedCategory);
        if (buttons == null) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].OptionIndex == optionIndex)
            {
                buttons[i].AnimateUnlock();
                break;
            }
        }
    }

    // ============================================================
    // PRIVATE HELPERS
    // ============================================================

    private bool IsItemUnlocked(AvatarCustomizationItem item)
    {
        // No catalog entry for this index yet -> treat as unlocked, so the picker
        // stays fully usable while the catalog is still being filled in.
        if (item == null)
            return true;

        switch (item.UnlockType)
        {
            case AvatarUnlockType.Free:
                return true;

            case AvatarUnlockType.BreakPoints:
                return PlayerManager.Instance != null && PlayerManager.Instance.IsCustomizationUnlocked(item.Id);

            case AvatarUnlockType.Level:
                return PlayerManager.Instance?.Profile != null
                    && PlayerManager.Instance.Profile.Level >= item.RequiredLevel;

            default:
                return true;
        }
    }

    private void HandleLockedOptionClicked(AvatarCustomizationItem item, int optionIndex)
    {
        if (item == null)
        {
            // Shouldn't happen (IsItemUnlocked already returns true for a null item),
            // but fall back to applying the selection rather than doing nothing.
            SelectOption(optionIndex);
            return;
        }

        // Locked interaction is intentionally sequential: SHAKE first, then the
        // relevant popup/message. The locked option itself receives no alpha/fade.
        if (item.UnlockType == AvatarUnlockType.Level)
        {
            if (!AnimateLockedFeedbackOnButton(
                    optionIndex,
                    () => ShowFeedback($"Disponível no nível {item.RequiredLevel}.")))
            {
                ShowFeedback($"Disponível no nível {item.RequiredLevel}.");
            }

            return;
        }

        if (item.UnlockType == AvatarUnlockType.BreakPoints)
        {
            if (!AnimateLockedFeedbackOnButton(
                    optionIndex,
                    () => OpenPurchaseConfirmation(item)))
            {
                OpenPurchaseConfirmation(item);
            }
        }
    }

    /// <summary>
    /// Plays the locked-feedback animation on the swatch button matching
    /// optionIndex in the currently selected category.
    /// Returns true when the matching button was found.
    /// </summary>
    private bool AnimateLockedFeedbackOnButton(
        int optionIndex,
        System.Action onComplete = null)
    {
        AvatarOptionButton[] buttons = GetButtonsForCategory(selectedCategory);
        if (buttons == null)
            return false;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].OptionIndex == optionIndex)
            {
                buttons[i].AnimateLockedFeedback(onComplete);
                return true;
            }
        }

        return false;
    }

    private void OpenPurchaseConfirmation(AvatarCustomizationItem item)
    {
        pendingPurchaseItem = item;

        if (purchaseConfirmPanel == null)
        {
            // No confirmation UI wired yet - attempt the purchase immediately so
            // the system stays testable before that panel exists in the scene.
            ConfirmPurchase();
            return;
        }

        if (purchaseConfirmLabel != null)
            purchaseConfirmLabel.text = $"Comprar {DisplayNameOrFallback(item)} por {item.BreakPointCost} PB?";

        AnimatePurchasePanelEnter();
    }

    /// <summary>
    /// Animates the purchase confirmation panel entrance with scale + fade.
    /// </summary>
    private void AnimatePurchasePanelEnter()
    {
        if (purchaseConfirmPanel == null) return;

        // Activate panel
        purchaseConfirmPanel.SetActive(true);

        // Setup CanvasGroup if not cached
        if (_purchasePanelCanvasGroup == null)
        {
            _purchasePanelCanvasGroup = purchaseConfirmPanel.GetComponent<CanvasGroup>();
            if (_purchasePanelCanvasGroup == null)
                _purchasePanelCanvasGroup = purchaseConfirmPanel.AddComponent<CanvasGroup>();
        }

        // Setup RectTransform if not cached
        if (_purchasePanelRect == null)
        {
            _purchasePanelRect = purchaseConfirmPanel.GetComponent<RectTransform>();
            if (_purchasePanelRect == null)
                _purchasePanelRect = purchaseConfirmPanel.GetComponentInParent<RectTransform>();
        }

        // Kill any existing tweens
        _purchasePanelCanvasGroup.DOKill();
        _purchasePanelRect?.DOKill();

        // Set initial state
        _purchasePanelCanvasGroup.alpha = 0f;
        _purchasePanelRect.localScale = Vector3.one * purchasePanelEnterScale;

        // Animate in
        var seq = DOTween.Sequence();
        seq.SetUpdate(UpdateType.Late);

        // Fade in
        seq.Join(_purchasePanelCanvasGroup.DOFade(1f, purchasePanelEnterDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(UpdateType.Late));

        // Scale down from overshoot
        seq.Join(_purchasePanelRect.DOScale(Vector3.one, purchasePanelEnterDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(UpdateType.Late));

        seq.Play();
    }

    /// <summary>
    /// Animates the purchase confirmation panel exit with scale + fade.
    /// </summary>
    private void AnimatePurchasePanelExit(System.Action onComplete = null)
    {
        if (purchaseConfirmPanel == null)
        {
            onComplete?.Invoke();
            return;
        }

        // Setup references if needed
        if (_purchasePanelCanvasGroup == null)
            _purchasePanelCanvasGroup = purchaseConfirmPanel.GetComponent<CanvasGroup>();
        if (_purchasePanelRect == null)
            _purchasePanelRect = purchaseConfirmPanel.GetComponent<RectTransform>();

        if (_purchasePanelCanvasGroup == null || _purchasePanelRect == null)
        {
            purchaseConfirmPanel.SetActive(false);
            onComplete?.Invoke();
            return;
        }

        // Kill any existing tweens
        _purchasePanelCanvasGroup.DOKill();
        _purchasePanelRect.DOKill();

        var seq = DOTween.Sequence();
        seq.SetUpdate(UpdateType.Late);

        // Fade out
        seq.Join(_purchasePanelCanvasGroup.DOFade(0f, purchasePanelExitDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(UpdateType.Late));

        // Scale up slightly before disappearing
        seq.Join(_purchasePanelRect.DOScale(Vector3.one * 0.95f, purchasePanelExitDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(UpdateType.Late));

        seq.OnComplete(() =>
        {
            purchaseConfirmPanel.SetActive(false);
            // Reset scale for next time
            _purchasePanelRect.localScale = Vector3.one;
            onComplete?.Invoke();
        });

        seq.Play();
    }

    private static string DisplayNameOrFallback(AvatarCustomizationItem item)
    {
        return string.IsNullOrWhiteSpace(item.DisplayName) ? item.Id : item.DisplayName;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackLabel == null)
            return;

        feedbackLabel.text = message;

        var cg = feedbackLabel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = feedbackLabel.gameObject.AddComponent<CanvasGroup>();

        // A new message owns the feedback label completely. Kill the previous
        // sequence before starting another one so delayed fade-outs cannot
        // overlap or hide a newer message.
        KillFeedbackTween();
        cg.alpha = 0f;

        _feedbackSequence = DOTween.Sequence();
        _feedbackSequence.SetUpdate(UpdateType.Late);
        _feedbackSequence.Append(cg.DOFade(1f, 0.2f).SetEase(Ease.OutQuad));
        _feedbackSequence.AppendInterval(1.5f);
        _feedbackSequence.Append(cg.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
        _feedbackSequence.OnComplete(() => _feedbackSequence = null);
        _feedbackSequence.Play();
    }

    private void KillFeedbackTween()
    {
        if (_feedbackSequence != null && _feedbackSequence.IsActive())
            _feedbackSequence.Kill();

        _feedbackSequence = null;

        // If the screen is disabled while a feedback message is mid-animation,
        // leave the temporary label in its neutral hidden state.
        if (feedbackLabel != null)
        {
            CanvasGroup cg = feedbackLabel.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = 0f;
        }
    }

    private void CopyFromSavedAvatar()
    {
        AvatarCustomizationData savedAvatar = PlayerManager.Instance?.Profile?.Avatar;
        if (savedAvatar == null)
            return;

        previewData.BodyIndex = savedAvatar.BodyIndex;
        previewData.HairIndex = savedAvatar.HairIndex;
        previewData.OutfitIndex = savedAvatar.OutfitIndex;
        previewData.AccessoryIndex = savedAvatar.AccessoryIndex;
    }

    private void ApplyPreview()
    {
        avatarPreview?.Apply(previewData);
    }

    /// <summary>
    /// Plays the customization option entrance as a spatial wave.
    /// The actual GridLayoutGroup remains untouched: we first force Unity to
    /// calculate the final layout, capture those positions, then animate each
    /// visible option from below back to its final position.
    ///
    /// Ordering is based on the real anchored positions, not the Hierarchy
    /// order. The bottom row is animated first, left-to-right, followed by the
    /// next row upward. This makes the effect resilient to GridLayoutGroup
    /// Start Corner / child ordering changes.
    /// </summary>
    private void PlayOptionWave()
    {
        if (!animateOptionWave)
            return;

        AvatarOptionButton[] buttons = GetButtonsForCategory(selectedCategory);
        if (buttons == null || buttons.Length == 0)
            return;

        KillOptionWave(true);

        Canvas.ForceUpdateCanvases();

        List<OptionWaveTarget> targets = new List<OptionWaveTarget>(buttons.Length);

        for (int i = 0; i < buttons.Length; i++)
        {
            AvatarOptionButton button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect == null)
                continue;

            CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = button.gameObject.AddComponent<CanvasGroup>();

            Vector2 finalPosition = rect.anchoredPosition;

            targets.Add(new OptionWaveTarget
            {
                RectTransform = rect,
                CanvasGroup = canvasGroup,
                FinalPosition = finalPosition,
                X = finalPosition.x,
                Y = finalPosition.y
            });
        }

        if (targets.Count == 0)
            return;

        _activeOptionWaveTargets.Clear();
        _activeOptionWaveTargets.AddRange(targets);

        // Sort by actual screen/layout position: bottom first, then left-to-right.
        targets.Sort((a, b) =>
        {
            int yCompare = a.Y.CompareTo(b.Y);
            if (yCompare != 0)
                return yCompare;

            return a.X.CompareTo(b.X);
        });

        _optionWaveSequence = DOTween.Sequence();
        _optionWaveSequence.SetUpdate(UpdateType.Late);

        float currentDelay = 0f;
        float previousY = targets[0].Y;

        for (int i = 0; i < targets.Count; i++)
        {
            OptionWaveTarget target = targets[i];

            // A meaningful Y change means the wave has moved to another row.
            // The GridLayoutGroup gives all items in the same row the same Y.
            if (i > 0)
            {
                bool newRow = !Mathf.Approximately(target.Y, previousY);
                currentDelay += newRow ? optionWaveRowDelay : optionWaveItemDelay;
            }

            previousY = target.Y;

            target.CanvasGroup.DOKill();
            target.RectTransform.DOKill();

            target.CanvasGroup.alpha = 0f;
            target.RectTransform.anchoredPosition =
                target.FinalPosition + Vector2.down * optionWaveStartOffset;

            Tween moveTween = target.RectTransform
                .DOAnchorPos(target.FinalPosition, optionWaveDuration)
                .SetEase(optionWaveEase)
                .SetUpdate(UpdateType.Late);

            Tween fadeTween = target.CanvasGroup
                .DOFade(1f, optionWaveDuration * 0.75f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(UpdateType.Late);

            _optionWaveSequence.Insert(currentDelay, moveTween);
            _optionWaveSequence.Insert(currentDelay, fadeTween);
        }

        _optionWaveSequence.OnComplete(() =>
        {
            for (int i = 0; i < targets.Count; i++)
            {
                OptionWaveTarget target = targets[i];
                if (target.RectTransform != null)
                    target.RectTransform.anchoredPosition = target.FinalPosition;
                if (target.CanvasGroup != null)
                    target.CanvasGroup.alpha = 1f;
            }

            _activeOptionWaveTargets.Clear();
            _optionWaveSequence = null;
        });

        _optionWaveSequence.Play();
    }

    private void KillOptionWave(bool restoreTargets)
    {
        if (_optionWaveSequence != null && _optionWaveSequence.IsActive())
        {
            _optionWaveSequence.Kill(false);
        }

        _optionWaveSequence = null;

        if (!restoreTargets)
        {
            _activeOptionWaveTargets.Clear();
            return;
        }

        for (int i = 0; i < _activeOptionWaveTargets.Count; i++)
        {
            OptionWaveTarget target = _activeOptionWaveTargets[i];
            if (target == null)
                continue;

            if (target.RectTransform != null)
            {
                target.RectTransform.DOKill();
                target.RectTransform.anchoredPosition = target.FinalPosition;
            }

            if (target.CanvasGroup != null)
            {
                target.CanvasGroup.DOKill();
                target.CanvasGroup.alpha = 1f;
            }
        }

        _activeOptionWaveTargets.Clear();
    }

    private void RefreshVisibleOptions()
    {
        SetOptionsVisible(hairOptions, selectedCategory == AvatarCustomizationCategory.Hair);
        SetOptionsVisible(outfitOptions, selectedCategory == AvatarCustomizationCategory.Outfit);
        SetOptionsVisible(accessoryOptions, selectedCategory == AvatarCustomizationCategory.Accessory);
    }

    private static void SetOptionsVisible(GameObject[] options, bool visible)
    {
        if (options == null)
            return;

        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != null)
                options[i].SetActive(visible);
        }
    }

    /// <summary>
    /// Updates the lock overlay/label on every swatch button in every category
    /// to match the current profile. Call after Start() and after any purchase.
    /// </summary>
    private void RefreshLockVisuals()
    {
        RefreshCategoryLockVisuals(AvatarCustomizationCategory.Hair, hairButtons);
        RefreshCategoryLockVisuals(AvatarCustomizationCategory.Outfit, outfitButtons);
        RefreshCategoryLockVisuals(AvatarCustomizationCategory.Accessory, accessoryButtons);
    }

    private void RefreshCategoryLockVisuals(AvatarCustomizationCategory category, AvatarOptionButton[] buttons)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            AvatarOptionButton optionButton = buttons[i];
            if (optionButton == null)
                continue;

            AvatarCustomizationItem item = catalog != null ? catalog.GetItem(category, optionButton.OptionIndex) : null;
            bool isLocked = !IsItemUnlocked(item);
            optionButton.SetLocked(isLocked, BuildLockLabel(item));
        }
    }

    private void CacheOptionButtons()
    {
        hairButtons = ExtractButtons(hairOptions);
        outfitButtons = ExtractButtons(outfitOptions);
        accessoryButtons = ExtractButtons(accessoryOptions);
    }

    private static AvatarOptionButton[] ExtractButtons(GameObject[] options)
    {
        if (options == null)
            return System.Array.Empty<AvatarOptionButton>();

        var buttons = new AvatarOptionButton[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != null)
                buttons[i] = options[i].GetComponent<AvatarOptionButton>();
        }

        return buttons;
    }

    private static string BuildLockLabel(AvatarCustomizationItem item)
    {
        if (item == null)
            return string.Empty;

        return item.UnlockType switch
        {
            AvatarUnlockType.BreakPoints => $"{item.BreakPointCost} PB",
            AvatarUnlockType.Level => $"Nv. {item.RequiredLevel}",
            _ => string.Empty
        };
    }

    private void BindButtons()
    {
        // Tabs remain wired persistently in the scene. Confirm is intentionally
        // bound here at runtime so there is one reliable source of truth for the
        // save action (the persistent Confirm callback is removed from the scene).
        confirmButton?.onClick.AddListener(Confirm);
        purchaseConfirmYesButton?.onClick.AddListener(ConfirmPurchase);
        purchaseConfirmNoButton?.onClick.AddListener(CancelPurchase);
    }

    private void UnbindButtons()
    {
        confirmButton?.onClick.RemoveListener(Confirm);
        purchaseConfirmYesButton?.onClick.RemoveListener(ConfirmPurchase);
        purchaseConfirmNoButton?.onClick.RemoveListener(CancelPurchase);
    }
}