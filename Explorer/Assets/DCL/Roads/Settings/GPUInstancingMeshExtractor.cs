using DCL.Rendering.GPUInstancing.InstancingData;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.Settings
{
    public class GPUInstancingMeshExtractor
    {
        private readonly struct MeshIdentifier : IEquatable<MeshIdentifier>
        {
            private readonly int vertexCount;
            private readonly int triangleCount;
            private readonly int subMeshCount;
            private readonly string shaderName;

            public MeshIdentifier(Mesh mesh, string shaderName)
            {
                vertexCount = mesh.vertexCount;
                triangleCount = mesh.triangles.Length;
                subMeshCount = mesh.subMeshCount;
                this.shaderName = shaderName;
            }

            public bool Equals(MeshIdentifier other) =>
                vertexCount == other.vertexCount &&
                triangleCount == other.triangleCount &&
                subMeshCount == other.subMeshCount &&
                shaderName == other.shaderName;

            public override bool Equals(object obj) =>
                obj is MeshIdentifier other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(vertexCount, triangleCount, subMeshCount, shaderName);
        }

        private class RendererGroup
        {
            public MeshIdentifier MeshId;
            public readonly List<RendererInfo> Renderers = new ();
        }

        private class RendererInfo
        {
            public readonly CombinedLodsRenderer Renderer;
            public readonly GPUInstancingLODGroupWithBuffer SourceGroup;
            public readonly Color MaterialColor;

            public RendererInfo(CombinedLodsRenderer renderer, GPUInstancingLODGroupWithBuffer sourceGroup)
            {
                Renderer = renderer;
                SourceGroup = sourceGroup;
                MaterialColor = renderer.SharedMaterial.color;
            }
        }

        public List<GPUInstancingLODGroupWithBuffer> ExtractSimilarMeshes(List<GPUInstancingLODGroupWithBuffer> originalGroups)
        {
            var extractedGroups = new List<GPUInstancingLODGroupWithBuffer>();
            List<RendererGroup> rendererGroups = GroupRenderersByMesh(originalGroups);

            foreach (RendererGroup rendererGroup in rendererGroups)
            {
                if (rendererGroup.Renderers.Count > 1)
                {
                    GPUInstancingLODGroupWithBuffer extractedGroup = CreateCombinedGroup(rendererGroup);
                    extractedGroups.Add(extractedGroup);
                    RemoveProcessedRenderers(rendererGroup.Renderers);
                }
            }

            // Удаляем пустые группы
            originalGroups.RemoveAll(group => group.CombinedLodsRenderers.Count == 0);

            return extractedGroups;
        }

        private List<RendererGroup> GroupRenderersByMesh(List<GPUInstancingLODGroupWithBuffer> groups)
        {
            var groupedRenderers = new Dictionary<MeshIdentifier, RendererGroup>();

            foreach (GPUInstancingLODGroupWithBuffer group in groups)
            foreach (CombinedLodsRenderer renderer in group.CombinedLodsRenderers)
            {
                var meshId = new MeshIdentifier(renderer.CombinedMesh, renderer.SharedMaterial.shader.name);

                if (!groupedRenderers.TryGetValue(meshId, out RendererGroup rendererGroup))
                {
                    rendererGroup = new RendererGroup { MeshId = meshId };
                    groupedRenderers[meshId] = rendererGroup;
                }

                rendererGroup.Renderers.Add(new RendererInfo(renderer, group));
            }

            return groupedRenderers.Values.ToList();
        }

        private GPUInstancingLODGroupWithBuffer CreateCombinedGroup(RendererGroup rendererGroup)
        {
            RendererInfo firstRenderer = rendererGroup.Renderers[0];
            var combinedInstances = new List<PerInstanceBuffer>();

            foreach (RendererInfo rendererInfo in rendererGroup.Renderers)
            foreach (PerInstanceBuffer instance in rendererInfo.SourceGroup.InstancesBuffer)
            {
                var newInstance = new PerInstanceBuffer(instance.instMatrix, instance.tiling, instance.offset)
                {
                    instColourTint = rendererInfo.MaterialColor,
                };

                combinedInstances.Add(newInstance);
            }

            return new GPUInstancingLODGroupWithBuffer(
                firstRenderer.Renderer.CombinedMesh.name,
                firstRenderer.SourceGroup.LODGroupData,
                firstRenderer.Renderer,
                combinedInstances);
        }

        private static void RemoveProcessedRenderers(List<RendererInfo> processedRenderers)
        {
            foreach (RendererInfo rendererInfo in processedRenderers)
                rendererInfo.SourceGroup.CombinedLodsRenderers.Remove(rendererInfo.Renderer);
        }
    }
}
