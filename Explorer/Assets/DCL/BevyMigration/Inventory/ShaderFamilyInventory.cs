using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.BevyMigration.Inventory
{
    /// <summary>
    ///     Opt-in diagnostic that records every shader bound by any renderer during a play session, with the distinct
    ///     materials and renderers seen per shader, and writes the report on quit. It is never active in a normal
    ///     session: the Editor menu or the shader-inventory app argument creates it. The periodic scan allocates;
    ///     that is accepted for a recording tool that is off by default.
    /// </summary>
    public class ShaderFamilyInventory : MonoBehaviour
    {
        private const float SCAN_INTERVAL_SECONDS = 1f;
        private const string GAME_OBJECT_NAME = "[BevyMigration] Shader Inventory";

        private static ShaderFamilyInventory? instance;

        private readonly Dictionary<string, ShaderUse> uses = new ();
        private readonly List<Material> materialsBuffer = new ();
        private readonly ReportData reportData = new (ReportCategory.DEBUG);

        private string outputPath = string.Empty;
        private float nextScanAt;

        public static bool IsRecording => instance != null;

        public static int ShadersSeen => instance != null ? instance.uses.Count : 0;

        /// <summary>
        ///     Starts recording (idempotent) and writes to <paramref name="path" /> on quit, on Stop, or when Write is called.
        /// </summary>
        public static ShaderFamilyInventory Start(string path)
        {
            ShaderFamilyInventory? recorder = instance;

            if (recorder == null)
            {
                var gameObject = new GameObject(GAME_OBJECT_NAME) { hideFlags = HideFlags.DontSave };
                DontDestroyOnLoad(gameObject);
                recorder = gameObject.AddComponent<ShaderFamilyInventory>();
                instance = recorder;
            }

            recorder.outputPath = path;
            ReportHub.Log(recorder.reportData, $"Shader inventory recording to {path}");
            return recorder;
        }

        public static void Stop()
        {
            if (instance == null) return;

            instance.Write();
            Destroy(instance.gameObject);
            instance = null;
        }

        public void Write()
        {
            Scan();
            var entries = new List<ShaderFamilyReport.Entry>(uses.Count);

            foreach (KeyValuePair<string, ShaderUse> use in uses)
                entries.Add(new ShaderFamilyReport.Entry(use.Key, use.Value.Materials.Count, use.Value.Renderers.Count));

            ShaderFamilyReport.Write(outputPath, "play session (runtime renderers)", entries);
            ReportHub.Log(reportData, $"Shader inventory written: {uses.Count} shaders to {outputPath}");
        }

        private void Update()
        {
            if (UnityEngine.Time.unscaledTime < nextScanAt) return;

            nextScanAt = UnityEngine.Time.unscaledTime + SCAN_INTERVAL_SECONDS;
            Scan();
        }

        private void OnApplicationQuit()
        {
            Write();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Scan()
        {
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);

            foreach (Renderer renderer in renderers)
            {
                renderer.GetSharedMaterials(materialsBuffer);

                foreach (Material material in materialsBuffer)
                {
                    if (material == null || material.shader == null) continue;

                    ShaderUse use = UseOf(material.shader.name);
                    use.Materials.Add(material.GetEntityId());
                    use.Renderers.Add(renderer.GetEntityId());
                }
            }
        }

        private ShaderUse UseOf(string shaderName)
        {
            if (uses.TryGetValue(shaderName, out ShaderUse? use))
                return use;

            use = new ShaderUse();
            uses[shaderName] = use;
            return use;
        }

        private class ShaderUse
        {
            public readonly HashSet<EntityId> Materials = new ();
            public readonly HashSet<EntityId> Renderers = new ();
        }
    }
}
