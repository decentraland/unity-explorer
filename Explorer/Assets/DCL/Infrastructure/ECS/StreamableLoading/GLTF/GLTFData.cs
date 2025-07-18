using DCL.Diagnostics;
using DCL.Profiling;
using GLTFast;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : StreamableRefCountData<GltfImport>
    {
        public readonly GameObject ContainerGameObject;
        public readonly GameObject RootGameObject;

        public GLTFData(GltfImport gltfImportedData, GameObject containerGameObject)
            : base(gltfImportedData, ReportCategory.GLTF_CONTAINER)
        {
            this.ContainerGameObject = containerGameObject;
            RootGameObject = containerGameObject.transform.GetChild(0).gameObject;
        }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.GltfDataAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.GltfReferencedAmount;

        protected override void DestroyObject()
        {
            // Dispose the GltfImport which will handle texture disposal
            Asset?.Dispose();

            // Destroy the container GameObject
            if (ContainerGameObject != null)
                Object.Destroy(ContainerGameObject);
        }
    }
}
