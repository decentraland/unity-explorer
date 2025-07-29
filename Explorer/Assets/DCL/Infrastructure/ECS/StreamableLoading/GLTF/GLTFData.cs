using DCL.Diagnostics;
using DCL.Profiling;
using GLTFast;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : StreamableRefCountData<GltfImport>
    {
        public readonly GameObject Root;
        public readonly IReadOnlyList<string>? HierarchyPaths;

        public GLTFData(GltfImport gltfImportedData, GameObject containerGameObject, IReadOnlyList<string>? hierarchyPaths = null)
            : base(gltfImportedData, ReportCategory.GLTF_CONTAINER)
        {
            if (containerGameObject == null) return;

            Root = containerGameObject;
            HierarchyPaths = hierarchyPaths;
        }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.GltfDataAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.GltfReferencedAmount;

        protected override void DestroyObject()
        {
            // Dispose the GltfImport which will handle texture disposal
            Asset?.Dispose();

            // Destroy the container GameObject
            if (Root != null)
                Object.Destroy(Root);
        }
    }
}
