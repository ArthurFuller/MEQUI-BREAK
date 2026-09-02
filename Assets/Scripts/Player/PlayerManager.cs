using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerManager : MonoBehaviour
{
    public const int DisplayNameMaxLength = 60;
    public const int StoreIdMaxLength = 50;
    public const int ShiftMaxLength = 30;

    public static PlayerManager Instance { get; private set; }

    [Header("Progressão")]
    [Tooltip("Total histórico de Break Points exigido por nível. O índice 0 representa o nível 1, o índice 1 representa o nível 2 e assim por diante.")]
    [SerializeField]
    private List<int> lifetimePointsPerLevel = new List<int> { 0, 100, 250, 450, 700, 1000 };

    public PlayerProfileData Profile { get; private set; }
    public bool HasValidRegistration => IsRegistrationValid(Profile);
    public string DisplayName => Profile?.DisplayName ?? string.Empty;
    public string StoreId => Profile?.StoreId ?? string.Empty;
    public string Shift => Profile?.Shift ?? string.Empty;
    public event System.Action<int> BreakPointsChanged;

    // Pontos pendentes para animação na entrada do HUB.
    public int PendingBreakPoints { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Initialize()
    {
        if (Profile == null)
        {
            Profile = SaveManager.Instance != null
                ? SaveManager.Instance.LoadProfile()
                : new PlayerProfileData();
        }

        NormalizeRegistrationData(Profile);
        MigrateLegacyProfile();
        RecalculateLevel();
    }

    /// <summary>
    /// Valida, normaliza e persiste Nome, Loja e Turno em uma única operação.
    /// Também pode ser reutilizado futuramente para editar o cadastro.
    /// </summary>
    public bool TryCompleteRegistration(
        string displayName,
        string storeId,
        string shift,
        out string errorMessage)
    {
        displayName = Normalize(displayName);
        storeId = Normalize(storeId);
        shift = Normalize(shift);

        if (!TryValidateRegistration(displayName, storeId, shift, out errorMessage))
            return false;

        Profile ??= new PlayerProfileData();

        string previousName = Profile.DisplayName;
        string previousStore = Profile.StoreId;
        string previousShift = Profile.Shift;
        bool previousCompletion = Profile.RegistrationCompleted;

        Profile.DisplayName = displayName;
        Profile.StoreId = storeId;
        Profile.Shift = shift;
        Profile.RegistrationCompleted = true;

        if (!TrySaveProfile())
        {
            Profile.DisplayName = previousName;
            Profile.StoreId = previousStore;
            Profile.Shift = previousShift;
            Profile.RegistrationCompleted = previousCompletion;
            errorMessage = "Não foi possível salvar o cadastro. Tente novamente.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Limpa somente os dados do cadastro, preservando progressão e avatar.
    /// Nenhuma tela oferece essa ação por enquanto.
    /// </summary>
    public bool ClearRegistration()
    {
        if (Profile == null)
            return true;

        string previousName = Profile.DisplayName;
        string previousStore = Profile.StoreId;
        string previousShift = Profile.Shift;
        bool previousCompletion = Profile.RegistrationCompleted;

        Profile.DisplayName = string.Empty;
        Profile.StoreId = string.Empty;
        Profile.Shift = string.Empty;
        Profile.RegistrationCompleted = false;

        if (TrySaveProfile())
            return true;

        Profile.DisplayName = previousName;
        Profile.StoreId = previousStore;
        Profile.Shift = previousShift;
        Profile.RegistrationCompleted = previousCompletion;
        return false;
    }

    public void SaveProfile() => TrySaveProfile();

    private bool TrySaveProfile()
    {
        return SaveManager.Instance != null
            && SaveManager.Instance.TrySaveProfile(Profile);
    }

    /// <summary>
    /// Confere os três valores obrigatórios sem depender de componentes visuais.
    /// </summary>
    public static bool TryValidateRegistration(
        string displayName,
        string storeId,
        string shift,
        out string errorMessage)
    {
        displayName = Normalize(displayName);
        storeId = Normalize(storeId);
        shift = Normalize(shift);

        if (displayName.Length == 0)
            errorMessage = "Informe seu nome.";
        else if (displayName.Length > DisplayNameMaxLength)
            errorMessage = $"O nome deve ter no máximo {DisplayNameMaxLength} caracteres.";
        else if (storeId.Length == 0)
            errorMessage = "Informe sua loja.";
        else if (storeId.Length > StoreIdMaxLength)
            errorMessage = $"A loja deve ter no máximo {StoreIdMaxLength} caracteres.";
        else if (shift.Length == 0)
            errorMessage = "Informe seu turno.";
        else if (shift.Length > ShiftMaxLength)
            errorMessage = $"O turno deve ter no máximo {ShiftMaxLength} caracteres.";
        else
        {
            errorMessage = string.Empty;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Concede Break Points ao saldo e ao total histórico, recalculando o nível.
    /// </summary>
    public void AddBreakPoints(int amount)
    {
        if (Profile == null || amount <= 0)
            return;

        Profile.BreakPoints += amount;
        Profile.LifetimeBreakPoints += amount;
        RecalculateLevel();
        BreakPointsChanged?.Invoke(Profile.BreakPoints);
    }

    /// <summary>
    /// Tenta gastar Break Points sem alterar o total histórico nem o nível.
    /// </summary>
    public bool TrySpendBreakPoints(int amount)
    {
        // Também funciona quando a cena de customização é aberta diretamente.
        if (Profile == null)
            Initialize();

        if (Profile == null || amount <= 0)
            return false;

        if (Profile.BreakPoints < amount)
            return false;

        Profile.BreakPoints -= amount;
        BreakPointsChanged?.Invoke(Profile.BreakPoints);
        return true;
    }

    public bool IsCustomizationUnlocked(string itemId)
    {
        return !string.IsNullOrEmpty(itemId)
            && Profile?.UnlockedCustomizationIds != null
            && Profile.UnlockedCustomizationIds.Contains(itemId);
    }

    public void UnlockCustomization(string itemId)
    {
        if (Profile == null || string.IsNullOrEmpty(itemId))
            return;

        Profile.UnlockedCustomizationIds ??= new List<string>();

        if (!Profile.UnlockedCustomizationIds.Contains(itemId))
            Profile.UnlockedCustomizationIds.Add(itemId);
    }

    private void RecalculateLevel()
    {
        if (Profile == null || lifetimePointsPerLevel == null || lifetimePointsPerLevel.Count == 0)
            return;

        int level = 1;
        for (int i = 0; i < lifetimePointsPerLevel.Count; i++)
        {
            if (Profile.LifetimeBreakPoints >= lifetimePointsPerLevel[i])
                level = i + 1;
        }

        // O nível deriva do total histórico, que nunca diminui.
        Profile.Level = level;
    }

    /// <summary>
    /// Migra perfis antigos usando o saldo existente como total histórico inicial.
    /// </summary>
    private void MigrateLegacyProfile()
    {
        if (Profile == null)
            return;

        Profile.UnlockedCustomizationIds ??= new List<string>();
        Profile.Avatar ??= new AvatarCustomizationData();

        // Perfis antigos não possuíam o campo do guia; o valor padrão zero
        // inicia a primeira etapa. Valores fora do intervalo são corrigidos
        // para evitar que um arquivo editado deixe o onboarding travado.
        if (Profile.OnboardingStep < 0 || Profile.OnboardingStep > 3)
            Profile.OnboardingStep = 0;

        if (Profile.LifetimeBreakPoints <= 0 && Profile.BreakPoints > 0)
            Profile.LifetimeBreakPoints = Profile.BreakPoints;
    }

    private static bool IsRegistrationValid(PlayerProfileData profile)
    {
        return profile != null
            && profile.RegistrationCompleted
            && TryValidateRegistration(
                profile.DisplayName,
                profile.StoreId,
                profile.Shift,
                out _);
    }

    private static void NormalizeRegistrationData(PlayerProfileData profile)
    {
        if (profile == null)
            return;

        profile.DisplayName = Normalize(profile.DisplayName);
        profile.StoreId = Normalize(profile.StoreId);
        profile.Shift = Normalize(profile.Shift);
    }

    private static string Normalize(string value) => value?.Trim() ?? string.Empty;

    /// <summary>
    /// Define a quantidade de pontos que será animada ao entrar no HUB.
    /// </summary>
    public void SetPendingPoints(int amount)
    {
        PendingBreakPoints = amount;
    }

    /// <summary>
    /// Limpa os pontos pendentes após a animação.
    /// </summary>
    public void ClearPendingPoints()
    {
        PendingBreakPoints = 0;
    }
}
