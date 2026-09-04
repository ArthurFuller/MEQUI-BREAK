#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Faz todo Play no Editor começar pelo Boot durante os testes.</summary>
[InitializeOnLoad]
public static class PlayModeStartSceneInstaller
{
    private const string BootScenePath = "Assets/Scenes/Boot/Boot.unity";

    static PlayModeStartSceneInstaller() => EditorApplication.delayCall += Configure;

    private static void Configure()
    {
        SceneAsset bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
        if (bootScene != null && EditorSceneManager.playModeStartScene != bootScene)
            EditorSceneManager.playModeStartScene = bootScene;
    }
}
#endif
