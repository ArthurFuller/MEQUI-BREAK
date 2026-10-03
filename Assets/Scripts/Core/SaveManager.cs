using System;
using System.IO;
using UnityEngine;

public sealed class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string ProfileFileName = "profile.json";
    private string ProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);
    public bool HasSavedProfile => TryReadProfile(ProfilePath, out _)
        || TryReadProfile(ProfilePath + ".bak", out _)
        || TryReadProfile(ProfilePath + ".tmp", out _);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Salva o perfil e informa se a gravação foi concluída.
    /// </summary>
    public bool TrySaveProfile(PlayerProfileData profile)
    {
        if (profile == null)
            return false;

        string pendingPath = ProfilePath + ".tmp";
        try
        {
            File.WriteAllText(pendingPath, JsonUtility.ToJson(profile));
            if (File.Exists(ProfilePath))
            {
                try { File.Replace(pendingPath, ProfilePath, ProfilePath + ".bak"); }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(ProfilePath, ProfilePath + ".bak", true);
                    File.Copy(pendingPath, ProfilePath, true);
                    File.Delete(pendingPath);
                }
            }
            else
                File.Move(pendingPath, ProfilePath);
            return true;
        }
        catch (Exception) { return false; }
    }

    public PlayerProfileData LoadProfile()
    {
        if (TryReadProfile(ProfilePath, out PlayerProfileData profile)
            || TryReadProfile(ProfilePath + ".bak", out profile)
            || TryReadProfile(ProfilePath + ".tmp", out profile))
            return profile;
        return new PlayerProfileData();
    }

    private static bool TryReadProfile(string path, out PlayerProfileData profile)
    {
        profile = null;
        if (!File.Exists(path)) return false;
        try
        {
            profile = JsonUtility.FromJson<PlayerProfileData>(File.ReadAllText(path));
            return profile != null;
        }
        catch (Exception) { return false; }
    }
}
