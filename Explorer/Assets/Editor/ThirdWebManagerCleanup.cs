using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor
{
    /// <summary>
    ///     Editor utility to remove ThirdWebManager from scenes after migration.
    ///     Run this once after the refactoring to clean up scenes.
    /// </summary>
    public static class ThirdWebManagerCleanup
    {
        [MenuItem("DCL/Web3/Cleanup ThirdWebManager from Scenes")]
        public static void CleanupThirdWebManagerFromScenes()
        {
            string[] scenePaths =
            {
                "Assets/Scenes/Main.unity",
                "Assets/ThirdWebUnity/Playground/ThirdWeb.Playground.unity",
            };

            var cleanedCount = 0;

            foreach (string scenePath in scenePaths)
            {
                if (!System.IO.File.Exists(scenePath))
                {
                    Debug.Log($"[ThirdWebCleanup] Scene not found: {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var foundInScene = false;

                // Find all GameObjects that might have ThirdWebManager
                // Since ThirdWebManager class is deleted, we search by component name in serialized data
                GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

                foreach (GameObject go in allObjects)
                {
                    // Check if this GameObject's name suggests it's the ThirdWebManager
                    if (go.name.Contains("ThirdWeb") || go.name.Contains("Thirdweb"))
                    {
                        // Check if it has a missing script (the deleted ThirdWebManager)
                        Component[] components = go.GetComponents<Component>();

                        foreach (Component comp in components)
                        {
                            if (comp == null) // Missing script
                            {
                                Debug.Log($"[ThirdWebCleanup] Found GameObject with missing script: {go.name} in {scenePath}");
                                Object.DestroyImmediate(go);
                                foundInScene = true;
                                cleanedCount++;
                                break;
                            }
                        }
                    }
                }

                if (foundInScene)
                {
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[ThirdWebCleanup] Saved scene: {scenePath}");
                }
                else { Debug.Log($"[ThirdWebCleanup] No ThirdWebManager found in: {scenePath}"); }
            }

            Debug.Log($"[ThirdWebCleanup] Cleanup completed. Removed {cleanedCount} objects.");

            if (cleanedCount == 0)
            {
                Debug.Log("[ThirdWebCleanup] No cleanup needed or ThirdWebManager GameObjects not found.");
                Debug.Log("[ThirdWebCleanup] If you still see errors, manually remove GameObjects with missing scripts from the scenes.");
            }
        }

        [MenuItem("DCL/Web3/Remove Missing Scripts from Current Scene")]
        public static void RemoveMissingScriptsFromCurrentScene()
        {
            var removedCount = 0;
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (GameObject go in allObjects)
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

                if (removed > 0)
                {
                    Debug.Log($"[ThirdWebCleanup] Removed {removed} missing script(s) from: {go.name}");
                    removedCount += removed;
                    EditorUtility.SetDirty(go);
                }
            }

            if (removedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[ThirdWebCleanup] Total removed: {removedCount} missing scripts. Remember to save the scene!");
            }
            else { Debug.Log("[ThirdWebCleanup] No missing scripts found in current scene."); }
        }
    }
}
