using System.Collections.Generic;
using System;
using UnityEngine;

public sealed class PlayerManager : MonoBehaviour
{
    private const int InitialBreakPoints = 500;

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
    public event System.Action EnergyStationAvailabilityChanged;

    // PB pendentes para a animação do HUB.
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
        bool hasSavedProfile = SaveManager.Instance != null
            && SaveManager.Instance.HasSavedProfile;

        if (Profile == null)
        {
            Profile = SaveManager.Instance != null
                ? SaveManager.Instance.LoadProfile()
                : new PlayerProfileData();
        }

        NormalizeRegistrationData(Profile);
        MigrateLegacyProfile();

        if (!Profile.BreakPointsInitialized)
        {
            if (!hasSavedProfile)
                Profile.BreakPoints = InitialBreakPoints;

            Profile.BreakPointsInitialized = true;
            TrySaveProfile();
        }

        RecalculateLevel();
    }

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

        if (!string.Equals(previousShift, Profile.Shift, StringComparison.OrdinalIgnoreCase))
            EnergyStationAvailabilityChanged?.Invoke();

        errorMessage = string.Empty;
        return true;
    }

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

    public bool CanPlayEnergyStation
    {
        get
        {
            if (Profile == null || !HasValidRegistration)
                return false;

            if (Profile.OnboardingStep < 3)
                return true;

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            return !string.Equals(Profile.LastEnergyStationCompletionDate, today, StringComparison.Ordinal);
        }
    }

    public bool MarkEnergyStationCompleted()
    {
        if (Profile == null)
            return false;

        string previousDate = Profile.LastEnergyStationCompletionDate;
        string previousShift = Profile.LastEnergyStationCompletionShift;

        Profile.LastEnergyStationCompletionDate = DateTime.Now.ToString("yyyy-MM-dd");
        Profile.LastEnergyStationCompletionShift = Shift;

        if (!TrySaveProfile())
        {
            Profile.LastEnergyStationCompletionDate = previousDate;
            Profile.LastEnergyStationCompletionShift = previousShift;
            return false;
        }

        EnergyStationAvailabilityChanged?.Invoke();
        return true;
    }

    public bool ResetTutorial()
    {
        if (Profile == null)
            return false;

        int previousStep = Profile.OnboardingStep;
        Profile.OnboardingStep = 0;

        if (TrySaveProfile())
            return true;

        Profile.OnboardingStep = previousStep;
        return false;
    }

    public bool ResetEnergyStation()
    {
        if (Profile == null)
            return false;

        string previousDate = Profile.LastEnergyStationCompletionDate;
        string previousShift = Profile.LastEnergyStationCompletionShift;
        Profile.LastEnergyStationCompletionDate = string.Empty;
        Profile.LastEnergyStationCompletionShift = string.Empty;

        if (!TrySaveProfile())
        {
            Profile.LastEnergyStationCompletionDate = previousDate;
            Profile.LastEnergyStationCompletionShift = previousShift;
            return false;
        }

        EnergyStationAvailabilityChanged?.Invoke();
        return true;
    }

    public bool TryUpdateWorkData(string storeId, string shift, out string errorMessage)
    {
        if (Profile == null || !HasValidRegistration)
        {
            errorMessage = "Cadastro inicial não encontrado.";
            return false;
        }

        return TryCompleteRegistration(DisplayName, storeId, shift, out errorMessage);
    }

    private bool TrySaveProfile()
    {
        return SaveManager.Instance != null
            && SaveManager.Instance.TrySaveProfile(Profile);
    }

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

    public void AddBreakPoints(int amount)
    {
        if (Profile == null || amount <= 0)
            return;

        Profile.BreakPoints += amount;
        Profile.LifetimeBreakPoints += amount;
        RecalculateLevel();
        BreakPointsChanged?.Invoke(Profile.BreakPoints);
    }

    public bool TrySpendBreakPoints(int amount)
    {
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

        // O nível usa o total histórico, não o saldo atual.
        Profile.Level = level;
    }

    private void MigrateLegacyProfile()
    {
        if (Profile == null)
            return;

        Profile.UnlockedCustomizationIds ??= new List<string>();
        Profile.Avatar ??= new AvatarCustomizationData();

        // Corrige valores antigos do tutorial para evitar save travado.
        if (Profile.OnboardingStep < 0 || Profile.OnboardingStep > 3)
            Profile.OnboardingStep = 0;

        if (!Profile.BreakPointsInitialized
            && Profile.LifetimeBreakPoints <= 0
            && Profile.BreakPoints > 0)
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

    public void SetPendingPoints(int amount)
    {
        PendingBreakPoints = amount;
    }

    public void ClearPendingPoints()
    {
        PendingBreakPoints = 0;
    }
}
