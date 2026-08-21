using UnityEngine;

public sealed class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

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
        return true;
    }

    public void SaveProfile() => SaveManager.Instance.SaveProfile(Profile);

    public void Logout()
    {
        IsLoggedIn = false;
        Profile = new PlayerProfileData();
    }

    public void AddBreakPoints(int amount)
    {
        if (Profile == null || amount <= 0)
            return;

        Profile.BreakPoints += amount;
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