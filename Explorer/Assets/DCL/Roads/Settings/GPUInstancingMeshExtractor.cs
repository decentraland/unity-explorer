using DCL.Rendering.GPUInstancing.InstancingData;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.Settings
{
    public class GPUInstancingMeshExtractor
    {
        private struct MeshIdentifier : IEquatable<MeshIdentifier>
        {
            public readonly int VertexCount;
            public readonly int TriangleCount;
            public readonly int SubMeshCount;
            public readonly string ShaderName;

            public MeshIdentifier(Mesh mesh, string shaderName)
            {
                VertexCount = mesh.vertexCount;
                TriangleCount = mesh.triangles.Length / 3;
                SubMeshCount = mesh.subMeshCount;
                ShaderName = shaderName;
            }

            public bool Equals(MeshIdentifier other) =>
                VertexCount == other.VertexCount &&
                TriangleCount == other.TriangleCount &&
                SubMeshCount == other.SubMeshCount &&
                ShaderName == other.ShaderName;

            public override bool Equals(object obj) =>
                obj is MeshIdentifier other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(VertexCount, TriangleCount, SubMeshCount, ShaderName);
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
            {
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
            }

            return groupedRenderers.Values.ToList();
        }

        private GPUInstancingLODGroupWithBuffer CreateCombinedGroup(RendererGroup rendererGroup)
        {
            RendererInfo firstRenderer = rendererGroup.Renderers[0];
            var combinedInstances = new List<PerInstanceBuffer>();

            // Объединяем InstancesBuffer из всех групп, добавляя цвет материала
            foreach (RendererInfo rendererInfo in rendererGroup.Renderers)
            {
                Color color = rendererInfo.MaterialColor;
                var colorVector = new Vector4(color.r, color.g, color.b, color.a);

                foreach (PerInstanceBuffer instance in rendererInfo.SourceGroup.InstancesBuffer)
                {
                    var newInstance = new PerInstanceBuffer(instance.instMatrix, instance.tiling, instance.offset)
                    {
                        instColourTint = colorVector,
                    };

                    combinedInstances.Add(newInstance);
                }
            }

            // Создаем название для объединенной группы
            var combinedName = $"Combined_{firstRenderer.Renderer.SharedMaterial.shader.name}_{rendererGroup.MeshId.VertexCount}v_{rendererGroup.MeshId.TriangleCount}t";

            return new GPUInstancingLODGroupWithBuffer(
                combinedName,
                firstRenderer.SourceGroup.LODGroupData,
                firstRenderer.Renderer,
                combinedInstances);
        }

        private void RemoveProcessedRenderers(List<RendererInfo> processedRenderers)
        {
            foreach (RendererInfo rendererInfo in processedRenderers) { rendererInfo.SourceGroup.CombinedLodsRenderers.Remove(rendererInfo.Renderer); }
        }
    }
}
