using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tanks.Complete
{
    public static class BuildTanksWebGL
    {
        [MenuItem("Tanks/🌐 Compilar Juego para WebGL")]
        public static void BuildGame()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string buildsDir = Path.Combine(projectRoot, "Builds");
            string webglDir = Path.Combine(buildsDir, "WebGL");
            string zipPath = Path.Combine(buildsDir, "Tanks_WebGL_Build.zip");

            if (!Directory.Exists(buildsDir))
            {
                Directory.CreateDirectory(buildsDir);
            }

            if (Directory.Exists(webglDir))
            {
                try
                {
                    Directory.Delete(webglDir, true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WebGL Build] No se pudo limpiar la carpeta previa: {e.Message}");
                }
            }
            Directory.CreateDirectory(webglDir);

            // Configure WebGL player settings for maximum compatibility
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // Uncompressed/Gzip fallback ensures 100% compatibility on Unity Play & web servers
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 600;

            string[] scenes = new string[]
            {
                "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Moon.unity",
                "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Desert.unity",
                "Assets/_Tanks/Tutorial_Demo/Demo_Scenes/Demo_Game_Jungle.unity"
            };

            Debug.Log("[WebGL Build] Iniciando compilación de WebGL...");

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = webglDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGL Build] ¡Éxito! Tamaño total: {summary.totalSize / (1024 * 1024)} MB");

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                ZipFile.CreateFromDirectory(webglDir, zipPath);
                Debug.Log($"[WebGL Build] ¡Archivo ZIP listo para Unity Play / entrega en: {zipPath}!");
            }
            else
            {
                Debug.LogError($"[WebGL Build] Error en la compilación WebGL: {summary.result}");
                throw new Exception($"WebGL Build Failed with result {summary.result}");
            }
        }
    }
}
