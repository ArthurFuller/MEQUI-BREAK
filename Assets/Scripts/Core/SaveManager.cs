using System.IO;
using UnityEngine;

public sealed class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string ProfileFileName = "profile.json";
    private string ProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SaveProfile(PlayerProfileData profile)
    {
        if (profile == null)
            return;

        File.WriteAllText(ProfilePath, JsonUtility.ToJson(profile));
    }

    public PlayerProfileData LoadProfile()
    {
        if (!File.Exists(ProfilePath))
            return new PlayerProfileData();

        string json = File.ReadAllText(ProfilePath);
        return string.IsNullOrWhiteSpace(json)
            ? new PlayerProfileData()
            : JsonUtility.FromJson<PlayerProfileData>(json) ?? new PlayerProfileData();
    }
}
