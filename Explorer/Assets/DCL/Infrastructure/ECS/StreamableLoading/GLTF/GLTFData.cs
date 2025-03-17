using DCL.Diagnostics;
using DCL.Profiling;
using GLTFast;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : StreamableRefCountData<GltfImport>
    {
        public readonly GameObject containerGameObject;

        public GLTFData(GltfImport gltfImportedData, GameObject containerGameObject)
            : base(gltfImportedData, ReportCategory.GLTF_CONTAINER)
        {
            this.containerGameObject = containerGameObject;
        }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.GltfDataAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.GltfReferencedAmount;

        protected override void DestroyObject()
        {
            // Dispose the GltfImport which will handle texture disposal
            Asset?.Dispose();

            // Destroy the container GameObject
            if (containerGameObject != null)
                Object.Destroy(containerGameObject);
        }
    }
}
