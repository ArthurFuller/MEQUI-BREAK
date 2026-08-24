using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Progression")]
    [Tooltip("Lifetime Break Points required for each level. Index 0 = Level 1 (usually 0), index 1 = Level 2, etc. Configurable so balancing never requires touching code.")]
    [SerializeField]
    private List<int> lifetimePointsPerLevel = new List<int> { 0, 100, 250, 450, 700, 1000 };

    public PlayerProfileData Profile { get; private set; }
    public bool IsLoggedIn { get; private set; }

    // Pending points to be animated when entering HUB
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
        Profile ??= SaveManager.Instance.LoadProfile();
        MigrateLegacyProfile();
        RecalculateLevel();
    }

    public bool Login(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return false;

        Profile ??= new PlayerProfileData();
        Profile.EmployeeId = employeeId.Trim();
        Profile.DisplayName = string.IsNullOrWhiteSpace(Profile.DisplayName) ? "Colaborador" : Profile.DisplayName;
        Profile.Role = string.IsNullOrWhiteSpace(Profile.Role) ? "Atendente" : Profile.Role;
        Profile.StoreId = string.IsNullOrWhiteSpace(Profile.StoreId) ? "DEMO-001" : Profile.StoreId;
        Profile.Shift = string.IsNullOrWhiteSpace(Profile.Shift) ? "Tarde" : Profile.Shift;
        IsLoggedIn = true;
        MigrateLegacyProfile();
        RecalculateLevel();
        return true;
    }

    public void SaveProfile() => SaveManager.Instance.SaveProfile(Profile);

    public void Logout()
    {
        IsLoggedIn = false;
        Profile = new PlayerProfileData();
    }

    /// <summary>
    /// Grants Break Points. Increases both the spendable balance and the lifetime
    /// total, and recalculates Level from the lifetime total (never from the
    /// spendable balance, so buying cosmetics can never lower the Level).
    /// </summary>
    public void AddBreakPoints(int amount)
    {
        if (Profile == null || amount <= 0)
            return;

        Profile.BreakPoints += amount;
        Profile.LifetimeBreakPoints += amount;
        RecalculateLevel();
    }

    /// <summary>
    /// Attempts to spend Break Points (e.g. buying a customization item).
    /// Only touches the spendable balance — LifetimeBreakPoints and Level are untouched.
    /// Returns false (and changes nothing) if the balance is insufficient.
    /// </summary>
    public bool TrySpendBreakPoints(int amount)
    {
        // The PlayerManager normally initializes from AppBootstrapper, but the
        // purchase flow must also be safe if Customization is opened directly
        // during development/testing.
        if (Profile == null)
            Initialize();

        if (Profile == null || amount <= 0)
            return false;

        if (Profile.BreakPoints < amount)
            return false;

        Profile.BreakPoints -= amount;
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

        // Level never decreases on its own — RecalculateLevel is only ever fed by
        // LifetimeBreakPoints, which itself never decreases.
        Profile.Level = level;
    }

    /// <summary>
    /// Profiles saved before LifetimeBreakPoints/UnlockedCustomizationIds existed
    /// deserialize with LifetimeBreakPoints = 0 even if they have a BreakPoints
    /// balance. Seed lifetime progress from the existing balance once, so returning
    /// players don't lose the Level progress they already effectively had.
    /// </summary>
    private void MigrateLegacyProfile()
    {
        if (Profile == null)
            return;

        Profile.UnlockedCustomizationIds ??= new List<string>();

        if (Profile.LifetimeBreakPoints <= 0 && Profile.BreakPoints > 0)
            Profile.LifetimeBreakPoints = Profile.BreakPoints;
    }

    /// <summary>
    /// Sets the amount of points to be animated when entering HUB
    /// </summary>
    public void SetPendingPoints(int amount)
    {
        PendingBreakPoints = amount;
    }

    /// <summary>
    /// Clears the pending points after animation is triggered
    /// </summary>
    public void ClearPendingPoints()
    {
        PendingBreakPoints = 0;
    }
}