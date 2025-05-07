using DCL.Diagnostics;
using DCL.Rendering.GPUInstancing.InstancingData;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.Utils
{
    public class LodsMeshCombiner
    {
        public readonly List<Renderer> RefRenderers = new ();

        private readonly Dictionary<(Material, Transform), CombinedLodsBuilder> combineDict = new ();
        private readonly Shader[] whitelistedShaders;
        private readonly Transform root;
        private readonly LOD[] lods;

        public LodsMeshCombiner(Shader[] whitelistedShaders, Transform root, LOD[] lods)
        {
            this.whitelistedShaders = whitelistedShaders;
            this.root = root;
            this.lods = lods;
        }

        public bool IsEmpty() =>
            combineDict.Count == 0;

        public void CollectCombineInstances()
        {
            foreach (LOD lod in lods)
            foreach (Renderer rend in lod.renderers)
            {
                MeshFilter mf = rend.GetComponent<MeshFilter>();

                if (!IsValidRenderer(rend, mf))
                    continue;

                for (var subMeshIndex = 0; subMeshIndex < rend.sharedMaterials.Length; subMeshIndex++)
                {
                    Material mat = rend.sharedMaterials[subMeshIndex];

                    if (!IsInWhitelist(mat, whitelistedShaders))
                        continue;

                    var ci = new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        subMeshIndex = subMeshIndex,

                        // Convert the renderer's transform into the local space of the prefab root.
                        transform = rend.transform.localToWorldMatrix * root.worldToLocalMatrix,
                    };

                    (Material mat, Transform parent) key = (mat, rend.transform.parent);

                    if (!combineDict.ContainsKey(key))
                        combineDict[key] = new CombinedLodsBuilder(mat, rend, subMeshIndex);

                    combineDict[key].AddCombineInstance(ci);

                    RefRenderers.Add(rend);
                }
            }
        }

        public List<CombinedLodsRenderer> BuildCombinedLodsRenderers() =>
            combineDict.Values.Select(combinedLodsBuilder => combinedLodsBuilder.Build(root.gameObject)).ToList();

        private static bool IsValidRenderer(Renderer rend, MeshFilter meshFilter)
        {
            if (rend is not MeshRenderer)
            {
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, $"Renderer '{rend.name}' is missing in LODGroup assigned renderers.");
                return false;
            }

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, $"Renderer '{rend.name}' is missing a MeshFilter or its mesh.");
                return false;
            }

            return true;
        }

        private static bool IsInWhitelist(Material material, Shader[] whitelistShaders)
        {
            if (material == null)
            {
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, "Renderer does not have a material or has not valid shader.");
                return false;
            }

            if (whitelistShaders == null || whitelistShaders.Length == 0)
            {
                ReportHub.LogError(ReportCategory.GPU_INSTANCING, "No whitelist shaders defined!");
                return false;
            }

            // DCL/Scene.shader was not always recognized during collection. So this is the only hack that worked.
            bool isValid = whitelistShaders.Where(shader => shader != null)
                                           .Any(shader => material.shader == shader ||
                                                          material.shader.name == shader.name ||
                                                          material.shader.name.StartsWith(shader.name) ||
                                                          shader.name.StartsWith(material.shader.name));

            if (!isValid)
            {
                ReportHub.LogWarning(ReportCategory.GPU_INSTANCING, "Renderer has not a valid shader.");
                return false;
            }

            return true;
        }
    }
}
