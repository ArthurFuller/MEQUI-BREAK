#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Packages Android native libraries uncompressed in the APK. This lets Android
/// load them from the APK instead of extracting a second copy into /data/app/lib.
/// </summary>
public sealed class AndroidNativeLibraryPackagingPostprocessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => int.MaxValue;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // Unity passes the unityLibrary module directory, not the Gradle root.
        string unityLibraryGradle = Path.Combine(path, "build.gradle");
        string gradleRoot = Directory.GetParent(path)?.FullName;
        string launcherGradle = gradleRoot == null
            ? null
            : Path.Combine(gradleRoot, "launcher", "build.gradle");

        int patchedModules = PatchGradleFile(unityLibraryGradle);
        if (launcherGradle != null)
            patchedModules += PatchGradleFile(launcherGradle);

        if (patchedModules == 0)
        {
            Debug.LogWarning(
                "Android native library packaging was not changed. " +
                "The expected Gradle modules or useLegacyPackaging setting were not found.");
            return;
        }

        Debug.Log(
            "Android native library packaging set to uncompressed in " +
            patchedModules + " Gradle module(s).");
    }

    private static int PatchGradleFile(string gradleFile)
    {
        if (string.IsNullOrEmpty(gradleFile) || !File.Exists(gradleFile))
            return 0;

        string original = File.ReadAllText(gradleFile);
        string updated = Regex.Replace(
            original,
            @"(?m)^(\s*useLegacyPackaging\s+)true(\s*)$",
            "$1false$2");

        if (updated == original)
            return 0;

        File.WriteAllText(gradleFile, updated);
        return 1;
    }
}
#endif