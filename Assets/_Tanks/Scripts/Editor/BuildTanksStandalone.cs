using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tanks.Complete
{
    public static class BuildTanksStandalone
    {
        [MenuItem("Tanks/⚡ Compilar Juego para Windows (.exe)")]
        public static void BuildGame()
        {
            string buildFolder = "C:/Unity/Tanks_Build";
            string exePath = Path.Combine(buildFolder, "TanksGame.exe");

            if (!Directory.Exists(buildFolder))
            {
                Directory.CreateDirectory(buildFolder);
            }

            string[] scenes = new string[]
            {
                "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Moon.unity",
                "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Desert.unity",
                "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Jungle.unity"
            };

            Debug.Log("[Tanks Build] Iniciando compilación de Windows Standalone...");

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Tanks Build] ¡Éxito! Tamaño del juego: {summary.totalSize / (1024 * 1024)} MB");

                // Comprimir en zip automáticamente para transferir
                string zipPath = "C:/Unity/Tanks_Game_Build.zip";
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                ZipFile.CreateFromDirectory(buildFolder, zipPath);
                Debug.Log($"[Tanks Build] ¡Archivo ZIP listo para transferir en: {zipPath}!");
            }
            else
            {
                Debug.LogError($"[Tanks Build] Error en la compilación: {summary.result}");
            }
        }
    }
}
