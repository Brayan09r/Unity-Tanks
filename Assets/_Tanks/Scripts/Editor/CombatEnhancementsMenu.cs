#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Menús de editor para instalar las Mejoras de Combate.
    ///
    ///  - GameObject > Tanks > Crear Mejoras de Combate:
    ///      crea el objeto "[Mejoras de Combate]" en la escena abierta.
    ///  - Tanks > Mejoras de Combate > Instalar en las escenas demo:
    ///      abre Demo_Game_Desert / Jungle / Moon, agrega o actualiza el objeto,
    ///      aplica los valores recomendados, sube el End Delay del GameManager y guarda.
    ///
    /// Solo agrega un objeto nuevo y cambia un valor del Inspector (End Delay);
    /// no modifica ningún script existente.
    /// </summary>
    public static class CombatEnhancementsMenu
    {
        private const string k_ObjectName = "[Mejoras de Combate]";
        private const string k_DemoScenesFolder = "Assets/_Tanks/Tutorial_Demo/Demo_Scenes";
        private const float k_RecommendedEndDelay = 6f;

        // ------------------------------------------------------------------ Escena abierta

        [MenuItem("GameObject/Tanks/Crear Mejoras de Combate", false, 11)]
        public static void CreateCombatEnhancements(MenuCommand menuCommand)
        {
            CombatTelemetry existing = Object.FindAnyObjectByType<CombatTelemetry>(FindObjectsInactive.Include);
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Mejoras de Combate",
                    "La escena ya tiene el objeto de mejoras (" + existing.gameObject.name + ").", "OK");
                Selection.activeObject = existing.gameObject;
                return;
            }

            GameObject root = CreateRoot();
            Undo.RegisterCreatedObjectUndo(root, "Crear Mejoras de Combate");
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeObject = root;

            Debug.Log("[Mejoras de Combate] Objeto creado. Guarda la escena (Ctrl+S) y dale Play.");
        }

        // ------------------------------------------------------------------ Todas las escenas demo

        [MenuItem("Tanks/Mejoras de Combate/Instalar en las escenas demo", false, 100)]
        public static void InstallInDemoScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Mejoras de Combate", "Sal del modo Play antes de instalar.", "OK");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { k_DemoScenesFolder });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Mejoras de Combate", "No se encontraron escenas en " + k_DemoScenesFolder, "OK");
                return;
            }

            // Guardamos lo abierto para no perder cambios y recordamos la escena actual.
            EditorSceneManager.SaveOpenScenes();
            string originalScene = EditorSceneManager.GetActiveScene().path;

            List<string> report = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                string result = ConfigureOpenScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                report.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + result);
            }

            if (!string.IsNullOrEmpty(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            string summary = "[Mejoras de Combate] Instalación terminada:\n  " + string.Join("\n  ", report.ToArray());
            Debug.Log(summary);
        }

        /// <summary>Agrega o completa el objeto de mejoras y aplica los valores recomendados.</summary>
        private static string ConfigureOpenScene()
        {
            CombatTelemetry telemetry = Object.FindAnyObjectByType<CombatTelemetry>(FindObjectsInactive.Include);
            GameObject root;
            string action;

            if (telemetry == null)
            {
                root = CreateRoot();
                action = "objeto creado";
            }
            else
            {
                root = telemetry.gameObject;
                EnsureComponent<MatchStatsTracker>(root);
                EnsureComponent<MatchStatsPanel>(root);
                EnsureComponent<FloatingTextSpawner>(root);
                action = "objeto actualizado";
            }

            ApplyRecommendedValues(root);

            GameManager manager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (manager != null)
            {
                SerializedObject so = new SerializedObject(manager);
                SerializedProperty endDelay = so.FindProperty("m_EndDelay");
                if (endDelay != null && endDelay.floatValue < k_RecommendedEndDelay)
                {
                    endDelay.floatValue = k_RecommendedEndDelay;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    action += ", End Delay = " + k_RecommendedEndDelay;
                }
            }
            else
            {
                action += " (sin GameManager)";
            }

            return action;
        }

        private static GameObject CreateRoot()
        {
            GameObject root = new GameObject(k_ObjectName);
            root.AddComponent<CombatTelemetry>();
            root.AddComponent<MatchStatsTracker>();
            root.AddComponent<MatchStatsPanel>();
            root.AddComponent<FloatingTextSpawner>();
            return root;
        }

        private static void EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null)
                go.AddComponent<T>();
        }

        /// <summary>Valores probados en el juego: panel compacto abajo y números cerca del tanque.</summary>
        private static void ApplyRecommendedValues(GameObject root)
        {
            MatchStatsPanel panel = root.GetComponent<MatchStatsPanel>();
            if (panel != null)
            {
                SerializedObject so = new SerializedObject(panel);
                SetFloat(so, "m_BottomMargin", 10f);
                SetFloat(so, "m_PanelScale", 0.8f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            FloatingTextSpawner spawner = root.GetComponent<FloatingTextSpawner>();
            if (spawner != null)
            {
                SerializedObject so = new SerializedObject(spawner);
                SetFloat(so, "m_FontSize", 14f);
                SetFloat(so, "m_HeightOffset", 1.6f);
                SetFloat(so, "m_RiseHeight", 1.3f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloat(SerializedObject so, string property, float value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.floatValue = value;
        }
    }
}
#endif
