using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SceneLoaderHelper
{
    private const string MoonScenePath = "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Moon.unity";
    private const string DesertScenePath = "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Desert.unity";
    private const string JungleScenePath = "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Jungle.unity";
    private const string OpenedFlag = "Temp/scene_auto_opened.flag";

    static SceneLoaderHelper()
    {
        EditorApplication.delayCall += CheckAndOpenDefaultScene;
    }

    private static void CheckAndOpenDefaultScene()
    {
        if (File.Exists(OpenedFlag)) return;

        var activeScene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path) || !activeScene.path.Contains("Demo_Game"))
        {
            OpenMoonScene();
            File.WriteAllText(OpenedFlag, "opened");
        }
    }

    [MenuItem("Tanks/Abrir Escena/1. Arena Lunar (Principal)")]
    public static void OpenMoonScene()
    {
        if (File.Exists(MoonScenePath))
        {
            EditorSceneManager.OpenScene(MoonScenePath, OpenSceneMode.Single);
            Debug.Log("[Tanks] Escena Lunar cargada con éxito.");
        }
    }

    [MenuItem("Tanks/Abrir Escena/2. Arena Desierto")]
    public static void OpenDesertScene()
    {
        if (File.Exists(DesertScenePath))
        {
            EditorSceneManager.OpenScene(DesertScenePath, OpenSceneMode.Single);
            Debug.Log("[Tanks] Escena Desierto cargada con éxito.");
        }
    }

    [MenuItem("Tanks/Abrir Escena/3. Arena Jungla")]
    public static void OpenJungleScene()
    {
        if (File.Exists(JungleScenePath))
        {
            EditorSceneManager.OpenScene(JungleScenePath, OpenSceneMode.Single);
            Debug.Log("[Tanks] Escena Jungla cargada con éxito.");
        }
    }
}
