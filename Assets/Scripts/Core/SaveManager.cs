using System.IO;
using UnityEngine;

public sealed class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string ProfileFileName = "profile.json";
    private string ProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);
    public bool HasSavedProfile => File.Exists(ProfilePath);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool TrySaveProfile(PlayerProfileData profile)
    {
        if (profile == null)
            return false;

        try
        {
            File.WriteAllText(ProfilePath, JsonUtility.ToJson(profile));
            return true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"Não foi possível salvar o perfil: {exception.Message}");
            return false;
        }
    }

    public PlayerProfileData LoadProfile()
    {
        try
        {
            if (!File.Exists(ProfilePath))
                return new PlayerProfileData();

            string json = File.ReadAllText(ProfilePath);
            return string.IsNullOrWhiteSpace(json)
                ? new PlayerProfileData()
                : JsonUtility.FromJson<PlayerProfileData>(json) ?? new PlayerProfileData();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"O perfil salvo é inválido ou não pôde ser lido: {exception.Message}");
            return new PlayerProfileData();
        }
    }
}
