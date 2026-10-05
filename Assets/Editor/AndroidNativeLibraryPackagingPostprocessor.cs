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
        string[] modules = { "launcher", "unityLibrary" };
        int patchedModules = 0;

        foreach (string module in modules)
        {
            string gradleFile = Path.Combine(path, module, "build.gradle");
            if (!File.Exists(gradleFile))
                continue;

            string original = File.ReadAllText(gradleFile);
            string updated = Regex.Replace(
                original,
                @"(?m)^(\s*useLegacyPackaging\s+)true(\s*)$",
                "$1false$2");

            if (updated == original)
                continue;

            File.WriteAllText(gradleFile, updated);
            patchedModules++;
        }

        if (patchedModules == 0)
        {
            Debug.LogWarning(
                "Android native library packaging was not changed. " +
                "The generated Gradle files no longer contain useLegacyPackaging true.");
            return;
        }

        Debug.Log(
            "Android native library packaging set to uncompressed in " +
            patchedModules + " Gradle module(s).");
    }
}
#endif