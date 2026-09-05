using DCL.Diagnostics;
using DCL.Profiling;
using GLTFast;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.GLTF
{
    public class GLTFData : StreamableRefCountData<GltfImport>
    {
        public readonly GameObject Root;
        public readonly IReadOnlyList<string>? HierarchyPaths;

        /// <summary>
        ///     External files fetched during the import mapped to the content URL each resolved to;
        ///     null when the download provider does not track them (global/realm loading).
        /// </summary>
        public readonly IReadOnlyList<GltfExternalDependency>? ExternalDependencies;

        public GLTFData(GltfImport gltfImportedData, GameObject containerGameObject, IReadOnlyList<string>? hierarchyPaths = null,
            IReadOnlyList<GltfExternalDependency>? externalDependencies = null)
            : base(gltfImportedData, ReportCategory.GLTF_CONTAINER)
        {
            ExternalDependencies = externalDependencies;

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

            // SafeDestroy routes to DestroyImmediate in edit mode (tests) and Destroy at runtime.
            UnityObjectUtils.SafeDestroy(Root);
        }
    }
}
