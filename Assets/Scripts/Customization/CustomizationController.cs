using DG.Tweening;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla a edição temporária do avatar, as regras de desbloqueio, a compra
/// de itens e a persistência da seleção após a confirmação do jogador.
/// </summary>
public sealed class CustomizationController : MonoBehaviour
{
    [Header("Cena")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private string profileScene = "Profile";
    [SerializeField] private string hubScene = "HUB";

    [Header("Pré-visualização")]
    [SerializeField] private AvatarView avatarPreview;

    [Header("Catálogo")]
    [Tooltip("Regras centrais de desbloqueio. Sem catálogo, todas as opções são consideradas gratuitas.")]
    [SerializeField] private AvatarCustomizationCatalog catalog;

    [Header("Controles")]
    [SerializeField] private Button hairButton;
    [SerializeField] private Button outfitButton;
    [SerializeField] private Button accessoryButton;
    [SerializeField] private Button confirmButton;

    [Header("Indicador de aba")]
    [SerializeField] private bool animateTabIndicator = true;
    [SerializeField, Min(1f)] private float tabIndicatorHeight = 5f;
    [SerializeField, Range(0.3f, 1f)] private float tabIndicatorWidthMultiplier = 0.72f;
    [SerializeField, Min(0f)] private float tabIndicatorBottomOffset = 5f;
    [SerializeField, Min(0.05f)] private float tabIndicatorDuration = 0.22f;
    [SerializeField] private Ease tabIndicatorEase = Ease.OutCubic;
    [SerializeField] private Color tabIndicatorColor = Color.white;

    [Header("Opções")]
    [SerializeField] private GameObject[] hairOptions;
    [SerializeField] private GameObject[] outfitOptions;
    [SerializeField] private GameObject[] accessoryOptions;

    [Header("Feedback (opcional)")]
    [SerializeField] private TMP_Text feedbackLabel;

    [Header("Confirmação de compra (opcional)")]
    [Tooltip("Sem painel, a compra com Break Points é tentada imediatamente ao clicar.")]
    [SerializeField] private GameObject purchaseConfirmPanel;
    [SerializeField] private TMP_Text purchaseConfirmLabel;
    [SerializeField] private Button purchaseConfirmYesButton;
    [SerializeField] private Button purchaseConfirmNoButton;

    [Header("Animação em onda da grade")]
    [Tooltip("Reproduz uma onda de baixo para cima quando uma categoria fica visível.")]
    [SerializeField] private bool animateOptionWave = true;

    [Tooltip("Duração do movimento de entrada de cada opção.")]
    [SerializeField, Min(0.05f)] private float optionWaveDuration = 0.32f;

    [Tooltip("Intervalo entre opções da mesma linha.")]
    [SerializeField, Min(0f)] private float optionWaveItemDelay = 0.045f;

    [Tooltip("Intervalo adicional quando a onda avança para a próxima linha.")]
    [SerializeField, Min(0f)] private float optionWaveRowDelay = 0.08f;

    [Tooltip("Distância abaixo da posição final em que cada opção começa.")]
    [SerializeField, Min(0f)] private float optionWaveStartOffset = 80f;

    [Tooltip("Curva usada no movimento de entrada das opções.")]
    [SerializeField] private Ease optionWaveEase = Ease.OutCubic;

    [Header("Animação do painel de compra")]
    [Tooltip("Duração da entrada do painel de compra.")]
    [SerializeField, Min(0.05f)] private float purchasePanelEnterDuration = 0.25f;

    [Tooltip("Duração da saída do painel de compra.")]
    [SerializeField, Min(0.05f)] private float purchasePanelExitDuration = 0.2f;

    [Tooltip("Fator de escala inicial da entrada do painel.")]
    [SerializeField, Min(1f)] private float purchasePanelEnterScale = 1.1f;

    private readonly AvatarCustomizationData previewData = new AvatarCustomizationData();
    private AvatarCustomizationCategory selectedCategory = AvatarCustomizationCategory.Hair;
    private AvatarCustomizationItem pendingPurchaseItem;

    // Referências armazenadas uma vez para evitar buscas durante atualizações visuais.
    private AvatarOptionButton[] hairButtons;
    private AvatarOptionButton[] outfitButtons;
    private AvatarOptionButton[] accessoryButtons;
    private OptionWaveTarget[] hairWaveTargets;
    private OptionWaveTarget[] outfitWaveTargets;
    private OptionWaveTarget[] accessoryWaveTargets;

    private RectTransform _purchasePanelRect;
    private CanvasGroup _purchasePanelCanvasGroup;
    private CanvasGroup _feedbackCanvasGroup;
    private Sequence _purchasePanelSequence;

    private Sequence _optionWaveSequence;
    private readonly List<OptionWaveTarget> _activeOptionWaveTargets = new List<OptionWaveTarget>();
    private readonly List<OptionWaveTarget> _optionWaveSortBuffer = new List<OptionWaveTarget>(12);

    private Sequence _feedbackSequence;

    // Indicador criado em runtime e ignorado pelo sistema de layout.
    private RectTransform _tabIndicator;
    private Sequence _tabIndicatorSequence;
    private bool _started;
    private bool _buttonsBound;

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
        // Oculta as opções antes do primeiro frame para evitar um flash antes da onda.
        PrepareOptionsHiddenForInitialWave(hairOptions);
        PrepareOptionsHiddenForInitialWave(outfitOptions);
        PrepareOptionsHiddenForInitialWave(accessoryOptions);
    }

    private void Start()
    {
        _started = true;
        CopyFromSavedAvatar();
        ApplyPreview();
        CacheOptionButtons();
        CacheOptionWaveTargets();
        BindButtons();
        RefreshVisibleOptions();
        RefreshLockVisuals();
        CachePanelReferences();

        CreateTabIndicator();
        if (animateTabIndicator || animateOptionWave)
            Canvas.ForceUpdateCanvases();
        UpdateTabIndicator(animate: false);

        StartCoroutine(PlayInitialOptionWaveWhenReady());

        // O painel de compra sempre começa oculto.
        if (purchaseConfirmPanel != null)
            purchaseConfirmPanel.SetActive(false);

    }


    /// <summary>
    /// Prepara as opções para a primeira onda sem desativar o cálculo do layout.
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

            // Apenas o CanvasGroup raiz é alterado; os filhos preservam seus alphas.
            canvasGroup.alpha = 0f;
        }
    }


    /// <summary>
    /// Aguarda a transição global terminar antes de iniciar a onda das opções.
    /// </summary>
    private IEnumerator PlayInitialOptionWaveWhenReady()
    {
        while (SceneLoader.IsTransitionInProgress)
            yield return null;

        // Aguarda um frame para o layout assumir suas posições finais.
        yield return null;
        PlayOptionWave();
    }

    private void OnDisable()
    {
        // Restaura alphas e posições se a tela sair durante a animação.
        KillOptionWave(true);
        KillFeedbackTween();
        KillPurchasePanelTween();
        UnbindButtons();
    }

    private void OnEnable()
    {
        if (_started)
            BindButtons();
    }

    private void OnDestroy()
    {
        KillOptionWave(false);
        KillFeedbackTween();
        KillPurchasePanelTween();

        if (_tabIndicatorSequence != null && _tabIndicatorSequence.IsActive())
            _tabIndicatorSequence.Kill(false);

        UnbindButtons();
    }

    /// <summary>
    /// Armazena as referências usadas nas animações temporárias.
    /// </summary>
    private void CachePanelReferences()
    {
        if (feedbackLabel != null)
        {
            _feedbackCanvasGroup = feedbackLabel.GetComponent<CanvasGroup>();
            if (_feedbackCanvasGroup == null)
                _feedbackCanvasGroup = feedbackLabel.gameObject.AddComponent<CanvasGroup>();
        }

        EnsurePurchasePanelReferences();
    }

    // Troca de categoria

    public void SelectHair()
    {
        SelectCategory(AvatarCustomizationCategory.Hair);
    }

    public void SelectOutfit()
    {
        SelectCategory(AvatarCustomizationCategory.Outfit);
    }

    public void SelectAccessory()
    {
        SelectCategory(AvatarCustomizationCategory.Accessory);
    }

    private void SelectCategory(AvatarCustomizationCategory category)
    {
        if (selectedCategory == category)
        {
            // Tocar novamente na aba ativa reinicia sua onda.
            PlayOptionWave();
            return;
        }

        selectedCategory = category;
        RefreshVisibleOptions();
        if (animateTabIndicator || animateOptionWave)
            Canvas.ForceUpdateCanvases();
        UpdateTabIndicator(animate: true);
        PlayOptionWave(forceLayout: false);
    }

    // Indicador de aba

    private void CreateTabIndicator()
    {
        if (!animateTabIndicator || hairButton == null)
            return;

        RectTransform parent = hairButton.transform.parent as RectTransform;
        if (parent == null)
            return;

        Transform existing = parent.Find("__TabSelectionIndicator");
        if (existing is RectTransform existingRect)
        {
            _tabIndicator = existingRect;
            return;
        }

        GameObject indicatorObject = new GameObject(
            "__TabSelectionIndicator",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement));

        _tabIndicator = indicatorObject.GetComponent<RectTransform>();
        _tabIndicator.SetParent(parent, false);
        _tabIndicator.anchorMin = new Vector2(0.5f, 0.5f);
        _tabIndicator.anchorMax = new Vector2(0.5f, 0.5f);
        _tabIndicator.pivot = new Vector2(0.5f, 0.5f);

        Image image = indicatorObject.GetComponent<Image>();
        TMP_Text tabLabel = hairButton.GetComponentInChildren<TMP_Text>(true);
        image.color = tabLabel != null ? tabLabel.color : tabIndicatorColor;
        image.raycastTarget = false;

        LayoutElement layoutElement = indicatorObject.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        _tabIndicator.SetAsLastSibling();
    }

    private void UpdateTabIndicator(bool animate)
    {
        if (!animateTabIndicator || _tabIndicator == null)
            return;

        Button targetButton = selectedCategory switch
        {
            AvatarCustomizationCategory.Hair => hairButton,
            AvatarCustomizationCategory.Outfit => outfitButton,
            AvatarCustomizationCategory.Accessory => accessoryButton,
            _ => hairButton
        };

        if (targetButton == null || targetButton.transform is not RectTransform targetRect)
            return;

        RectTransform parent = _tabIndicator.parent as RectTransform;
        if (parent == null)
            return;

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, targetRect);

        Vector2 targetPosition = new Vector2(
            bounds.center.x,
            bounds.min.y - tabIndicatorBottomOffset);

        Vector2 targetSize = new Vector2(
            Mathf.Max(1f, bounds.size.x * tabIndicatorWidthMultiplier),
            tabIndicatorHeight);

        if (_tabIndicatorSequence != null && _tabIndicatorSequence.IsActive())
            _tabIndicatorSequence.Kill(false);

        _tabIndicatorSequence = null;

        if (!animate)
        {
            _tabIndicator.anchoredPosition = targetPosition;
            _tabIndicator.sizeDelta = targetSize;
            return;
        }

        _tabIndicatorSequence = DOTween.Sequence()
            .SetUpdate(UpdateType.Late)
            .Join(
                _tabIndicator
                    .DOAnchorPos(targetPosition, tabIndicatorDuration)
                    .SetEase(tabIndicatorEase)
                    .SetUpdate(UpdateType.Late))
            .Join(
                _tabIndicator
                    .DOSizeDelta(targetSize, tabIndicatorDuration)
                    .SetEase(tabIndicatorEase)
                    .SetUpdate(UpdateType.Late));
    }

    /// <summary>
    /// Valida as regras do catálogo antes de selecionar ou oferecer a compra.
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
    /// Aplica uma opção já validada à pré-visualização mantida em memória.
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

        AnimateSelectionConfirmed(optionIndex);
    }

    /// <summary>
    /// Anima o botão correspondente à seleção confirmada.
    /// </summary>
    private void AnimateSelectionConfirmed(int optionIndex)
    {
        FindButton(optionIndex)?.AnimateSelectionConfirmed();
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

    private AvatarOptionButton FindButton(int optionIndex)
    {
        AvatarOptionButton[] buttons = GetButtonsForCategory(selectedCategory);
        if (buttons == null)
            return null;

        for (int i = 0; i < buttons.Length; i++)
        {
            AvatarOptionButton button = buttons[i];
            if (button != null && button.OptionIndex == optionIndex)
                return button;
        }

        return null;
    }

    // Confirmação e cancelamento

    public void Confirm()
    {
        PlayerManager player = PlayerManager.Instance;
        if (player == null)
        {
            Debug.LogError("CustomizationController.Confirm: PlayerManager.Instance is null. Avatar was not saved.");
            return;
        }

        // Garante compatibilidade ao abrir a cena diretamente ou carregar perfil antigo.
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
        // Como previewData ainda não foi persistido, sair descarta as alterações.
        AudioManager.Instance?.PlayClick();

        sceneLoader?.Load(hubScene);
    }

    public void Back() => Cancel();



    /// <summary>Confirma a compra pendente com Break Points.</summary>
    public void ConfirmPurchase()
    {
        // Preserva o item localmente até a transação terminar.
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

        if (item.UnlockType != AvatarUnlockType.BreakPoints)
        {
            pendingPurchaseItem = null;
            AnimatePurchasePanelExit();
            return;
        }

        if (!player.TrySpendBreakPoints(item.BreakPointCost))
        {
            ShowFeedback($"PB insuficiente. Necessário: {item.BreakPointCost} PB.");
            pendingPurchaseItem = null;
            AnimatePurchasePanelExit();
            RefreshLockVisuals();
            return;
        }

        player.UnlockCustomization(item.Id);
        player.SaveProfile();
        pendingPurchaseItem = null;

        AnimatePurchasePanelExit(() =>
        {
            SelectOption(item.OptionIndex);
            RefreshLockVisuals();
            ShowFeedback($"{DisplayNameOrFallback(item)} desbloqueado!");
            AnimateUnlockedButton(item.OptionIndex);
        });
    }

    /// <summary>Cancela a compra pendente.</summary>
    public void CancelPurchase()
    {
        pendingPurchaseItem = null;
        AnimatePurchasePanelExit();
    }

    /// <summary>
    /// Anima o botão desbloqueado após a compra.
    /// </summary>
    private void AnimateUnlockedButton(int optionIndex)
    {
        FindButton(optionIndex)?.AnimateUnlock();
    }

    private bool IsItemUnlocked(AvatarCustomizationItem item)
    {
        // Sem entrada no catálogo, a opção continua disponível.
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
            SelectOption(optionIndex);
            return;
        }

        // O balanço termina antes da mensagem ou painel correspondente aparecer.
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
    /// Anima a tentativa bloqueada e informa se o botão correspondente existe.
    /// </summary>
    private bool AnimateLockedFeedbackOnButton(
        int optionIndex,
        System.Action onComplete = null)
    {
        AvatarOptionButton button = FindButton(optionIndex);
        if (button == null)
            return false;

        button.AnimateLockedFeedback(onComplete);
        return true;
    }

    private void OpenPurchaseConfirmation(AvatarCustomizationItem item)
    {
        pendingPurchaseItem = item;

        if (purchaseConfirmPanel == null)
        {
            // Sem interface de confirmação, tenta a compra imediatamente.
            ConfirmPurchase();
            return;
        }

        if (purchaseConfirmLabel != null)
            purchaseConfirmLabel.text = $"Comprar {DisplayNameOrFallback(item)} por {item.BreakPointCost} PB?";

        AnimatePurchasePanelEnter();
    }

    /// <summary>
    /// Anima a entrada do painel de compra com escala e transparência.
    /// </summary>
    private void AnimatePurchasePanelEnter()
    {
        if (purchaseConfirmPanel == null) return;

        purchaseConfirmPanel.SetActive(true);

        if (!EnsurePurchasePanelReferences())
            return;

        KillPurchasePanelTween();
        _purchasePanelCanvasGroup.DOKill();
        _purchasePanelRect?.DOKill();

        _purchasePanelCanvasGroup.alpha = 0f;
        _purchasePanelRect.localScale = Vector3.one * purchasePanelEnterScale;

        _purchasePanelSequence = DOTween.Sequence();
        _purchasePanelSequence.SetUpdate(UpdateType.Late);

        _purchasePanelSequence.Join(_purchasePanelCanvasGroup.DOFade(1f, purchasePanelEnterDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(UpdateType.Late));

        _purchasePanelSequence.Join(_purchasePanelRect.DOScale(Vector3.one, purchasePanelEnterDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(UpdateType.Late));

        _purchasePanelSequence.OnComplete(() => _purchasePanelSequence = null);
        _purchasePanelSequence.Play();
    }

    /// <summary>
    /// Anima a saída do painel de compra com escala e transparência.
    /// </summary>
    private void AnimatePurchasePanelExit(System.Action onComplete = null)
    {
        if (purchaseConfirmPanel == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (!EnsurePurchasePanelReferences())
        {
            purchaseConfirmPanel.SetActive(false);
            onComplete?.Invoke();
            return;
        }

        KillPurchasePanelTween();
        _purchasePanelCanvasGroup.DOKill();
        _purchasePanelRect.DOKill();

        _purchasePanelSequence = DOTween.Sequence();
        _purchasePanelSequence.SetUpdate(UpdateType.Late);

        _purchasePanelSequence.Join(_purchasePanelCanvasGroup.DOFade(0f, purchasePanelExitDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(UpdateType.Late));

        _purchasePanelSequence.Join(_purchasePanelRect.DOScale(Vector3.one * 0.95f, purchasePanelExitDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(UpdateType.Late));

        _purchasePanelSequence.OnComplete(() =>
        {
            _purchasePanelSequence = null;
            purchaseConfirmPanel.SetActive(false);
            _purchasePanelRect.localScale = Vector3.one;
            onComplete?.Invoke();
        });

        _purchasePanelSequence.Play();
    }

    private void KillPurchasePanelTween()
    {
        if (_purchasePanelSequence != null && _purchasePanelSequence.IsActive())
            _purchasePanelSequence.Kill(false);

        _purchasePanelSequence = null;
    }

    private bool EnsurePurchasePanelReferences()
    {
        if (purchaseConfirmPanel == null)
            return false;

        _purchasePanelRect ??= purchaseConfirmPanel.GetComponent<RectTransform>()
            ?? purchaseConfirmPanel.GetComponentInParent<RectTransform>();
        _purchasePanelCanvasGroup ??= purchaseConfirmPanel.GetComponent<CanvasGroup>()
            ?? purchaseConfirmPanel.AddComponent<CanvasGroup>();

        return _purchasePanelRect != null && _purchasePanelCanvasGroup != null;
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

        if (_feedbackCanvasGroup == null)
        {
            _feedbackCanvasGroup = feedbackLabel.GetComponent<CanvasGroup>();
            if (_feedbackCanvasGroup == null)
                _feedbackCanvasGroup = feedbackLabel.gameObject.AddComponent<CanvasGroup>();
        }

        // A mensagem nova substitui completamente qualquer sequência anterior.
        KillFeedbackTween();
        _feedbackCanvasGroup.alpha = 0f;

        _feedbackSequence = DOTween.Sequence();
        _feedbackSequence.SetUpdate(UpdateType.Late);
        _feedbackSequence.Append(_feedbackCanvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad));
        _feedbackSequence.AppendInterval(1.5f);
        _feedbackSequence.Append(_feedbackCanvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
        _feedbackSequence.OnComplete(() => _feedbackSequence = null);
        _feedbackSequence.Play();
    }

    private void KillFeedbackTween()
    {
        if (_feedbackSequence != null && _feedbackSequence.IsActive())
            _feedbackSequence.Kill();

        _feedbackSequence = null;

        // Ao sair da tela, o texto temporário volta ao estado oculto.
        if (_feedbackCanvasGroup != null)
            _feedbackCanvasGroup.alpha = 0f;
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
    /// Anima as opções visíveis de baixo para cima usando suas posições reais no layout.
    /// </summary>
    private void PlayOptionWave(bool forceLayout = true)
    {
        if (!animateOptionWave)
            return;

        OptionWaveTarget[] cachedTargets = GetWaveTargetsForCategory(selectedCategory);
        if (cachedTargets == null || cachedTargets.Length == 0)
            return;

        KillOptionWave(true);

        if (forceLayout)
            Canvas.ForceUpdateCanvases();

        _optionWaveSortBuffer.Clear();

        for (int i = 0; i < cachedTargets.Length; i++)
        {
            OptionWaveTarget target = cachedTargets[i];
            if (target == null || target.RectTransform == null ||
                !target.RectTransform.gameObject.activeInHierarchy)
                continue;

            target.FinalPosition = target.RectTransform.anchoredPosition;
            target.X = target.FinalPosition.x;
            target.Y = target.FinalPosition.y;
            _optionWaveSortBuffer.Add(target);
        }

        if (_optionWaveSortBuffer.Count == 0)
            return;

        _activeOptionWaveTargets.Clear();
        _activeOptionWaveTargets.AddRange(_optionWaveSortBuffer);

        // Ordena de baixo para cima e, em cada linha, da esquerda para a direita.
        _optionWaveSortBuffer.Sort(CompareWaveTargets);

        _optionWaveSequence = DOTween.Sequence();
        _optionWaveSequence.SetUpdate(UpdateType.Late);

        float currentDelay = 0f;
        float previousY = _optionWaveSortBuffer[0].Y;

        for (int i = 0; i < _optionWaveSortBuffer.Count; i++)
        {
            OptionWaveTarget target = _optionWaveSortBuffer[i];

            // Uma mudança em Y indica o avanço para outra linha.
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
            for (int i = 0; i < _activeOptionWaveTargets.Count; i++)
            {
                OptionWaveTarget target = _activeOptionWaveTargets[i];
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

    private static int CompareWaveTargets(OptionWaveTarget a, OptionWaveTarget b)
    {
        int yCompare = a.Y.CompareTo(b.Y);
        return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
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
            if (options[i] != null && options[i].activeSelf != visible)
                options[i].SetActive(visible);
        }
    }

    /// <summary>
    /// Atualiza os visuais de bloqueio de todas as categorias.
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

    private void CacheOptionWaveTargets()
    {
        if (!animateOptionWave)
        {
            hairWaveTargets = System.Array.Empty<OptionWaveTarget>();
            outfitWaveTargets = System.Array.Empty<OptionWaveTarget>();
            accessoryWaveTargets = System.Array.Empty<OptionWaveTarget>();
            return;
        }

        hairWaveTargets = BuildWaveTargets(hairButtons);
        outfitWaveTargets = BuildWaveTargets(outfitButtons);
        accessoryWaveTargets = BuildWaveTargets(accessoryButtons);
    }

    private static OptionWaveTarget[] BuildWaveTargets(AvatarOptionButton[] buttons)
    {
        if (buttons == null || buttons.Length == 0)
            return System.Array.Empty<OptionWaveTarget>();

        OptionWaveTarget[] targets = new OptionWaveTarget[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            AvatarOptionButton button = buttons[i];
            if (button == null)
                continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = button.gameObject.AddComponent<CanvasGroup>();

            targets[i] = new OptionWaveTarget
            {
                RectTransform = rect,
                CanvasGroup = canvasGroup
            };
        }

        return targets;
    }

    private OptionWaveTarget[] GetWaveTargetsForCategory(AvatarCustomizationCategory category)
    {
        return category switch
        {
            AvatarCustomizationCategory.Hair => hairWaveTargets,
            AvatarCustomizationCategory.Outfit => outfitWaveTargets,
            AvatarCustomizationCategory.Accessory => accessoryWaveTargets,
            _ => null
        };
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
        if (_buttonsBound)
            return;

        // Confirmação e compra possuem uma única origem de listeners em runtime.
        confirmButton?.onClick.AddListener(Confirm);
        purchaseConfirmYesButton?.onClick.AddListener(ConfirmPurchase);
        purchaseConfirmNoButton?.onClick.AddListener(CancelPurchase);
        _buttonsBound = true;
    }

    private void UnbindButtons()
    {
        if (!_buttonsBound)
            return;

        confirmButton?.onClick.RemoveListener(Confirm);
        purchaseConfirmYesButton?.onClick.RemoveListener(ConfirmPurchase);
        purchaseConfirmNoButton?.onClick.RemoveListener(CancelPurchase);
        _buttonsBound = false;
    }
}
