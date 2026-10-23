using DCL.BevyMigration.Inventory;
using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DCL.BevyMigration.Editor
{
    /// <summary>
    ///     Editor entry points of the shader inventory (BevyMigration/docs/README.md § 7, step 1): record a play
    ///     session, or scan the project's materials and prefabs for the static baseline. Both write
    ///     BevyMigration/ledger/shader-families.md.
    /// </summary>
    public static class ShaderFamilyInventoryMenu
    {
        private const string MENU = "Decentraland/Bevy Migration/Shader Inventory/";
        private const string MATERIAL_FILTER = "t:Material";
        private const string PREFAB_FILTER = "t:Prefab";

        private static readonly ReportData REPORT_DATA = new (ReportCategory.DEBUG);

        [MenuItem(MENU + "Start Recording (Play Mode)")]
        public static void StartRecording()
        {
            ShaderFamilyInventory.Start(ShaderFamilyReport.LedgerPath);
        }

        [MenuItem(MENU + "Start Recording (Play Mode)", true)]
        public static bool StartRecordingEnabled() =>
            Application.isPlaying && !ShaderFamilyInventory.IsRecording;

        [MenuItem(MENU + "Write shader-families.md and Stop")]
        public static void StopRecording()
        {
            ShaderFamilyInventory.Stop();
        }

        [MenuItem(MENU + "Write shader-families.md and Stop", true)]
        public static bool StopRecordingEnabled() =>
            ShaderFamilyInventory.IsRecording;

        /// <summary>
        ///     Static baseline: every material asset under Assets and Packages counted by shader, and every renderer
        ///     inside a prefab counted against the shaders of its shared materials. Scene content that only exists at
        ///     runtime (glTF and asset-bundle materials, avatars) is not here; the play-session recording covers it.
        /// </summary>
        [MenuItem(MENU + "Scan Project Assets")]
        public static void ScanProjectAssets()
        {
            string path = ShaderFamilyReport.LedgerPath;
            ShaderFamilyReport.Write(path, "project assets (material and prefab scan, no runtime content)", CollectProjectEntries());
            ReportHub.Log(REPORT_DATA, $"Shader inventory of project assets written to {path}");
        }

        /// <summary>
        ///     Batch-mode entry: <c>Unity -batchmode -executeMethod DCL.BevyMigration.Editor.ShaderFamilyInventoryMenu.ScanProjectAssetsBatch -quit</c>.
        /// </summary>
        public static void ScanProjectAssetsBatch()
        {
            ScanProjectAssets();
            EditorApplication.Exit(0);
        }

        private static List<ShaderFamilyReport.Entry> CollectProjectEntries()
        {
            var materialsByShader = new Dictionary<string, HashSet<EntityId>>();
            var renderersByShader = new Dictionary<string, HashSet<EntityId>>();

            foreach (string guid in AssetDatabase.FindAssets(MATERIAL_FILTER))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || material.shader == null) continue;

                Add(materialsByShader, material.shader.name, material.GetEntityId());
            }

            var sharedMaterials = new List<Material>();

            foreach (string guid in AssetDatabase.FindAssets(PREFAB_FILTER))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;

                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.GetSharedMaterials(sharedMaterials);

                    foreach (Material material in sharedMaterials)
                    {
                        if (material == null || material.shader == null) continue;

                        Add(materialsByShader, material.shader.name, material.GetEntityId());
                        Add(renderersByShader, material.shader.name, renderer.GetEntityId());
                    }
                }
            }

            var entries = new List<ShaderFamilyReport.Entry>(materialsByShader.Count);

            foreach (KeyValuePair<string, HashSet<EntityId>> shader in materialsByShader)
                entries.Add(new ShaderFamilyReport.Entry(shader.Key, shader.Value.Count, renderersByShader.TryGetValue(shader.Key, out HashSet<EntityId> renderers) ? renderers.Count : 0));

            return entries;
        }

        private static void Add(Dictionary<string, HashSet<EntityId>> byShader, string shaderName, EntityId entityId)
        {
            if (!byShader.TryGetValue(shaderName, out HashSet<EntityId> ids))
            {
                ids = new HashSet<EntityId>();
                byShader[shaderName] = ids;
            }

            ids.Add(entityId);
        }
    }
}
