#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Tanks.Complete
{
    public static class DestructibleBoxMenu
    {
        [MenuItem("GameObject/Tanks/Create Destructible Box", false, 10)]
        public static void CreateDestructibleBox(MenuCommand menuCommand)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "DestructibleBox";
            box.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            // Position at scene view pivot or (0, 0.75, 0)
            if (SceneView.lastActiveSceneView != null)
            {
                Vector3 spawnPos = SceneView.lastActiveSceneView.pivot;
                spawnPos.y = 0.75f;
                box.transform.position = spawnPos;
            }
            else
            {
                box.transform.position = new Vector3(0f, 0.75f, 0f);
            }

            // Assign environment material if available
            string[] guids = AssetDatabase.FindAssets("Brown t:Material", new[] { "Assets/_Tanks/Art/Materials" });
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("OilDrums t:Material", new[] { "Assets/_Tanks/Art/Materials" });

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                    box.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            // Setup NavMeshObstacle with carving
            var navObstacle = box.AddComponent<NavMeshObstacle>();
            navObstacle.carving = true;
            navObstacle.carveOnlyStationary = false;
            navObstacle.size = new Vector3(1.5f, 1.5f, 1.5f);

            // Add DestructibleBox component
            box.AddComponent<DestructibleBox>();

            GameObjectUtility.SetParentAndAlign(box, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(box, "Create " + box.name);
            Selection.activeObject = box;
        }
    }
}
#endif
