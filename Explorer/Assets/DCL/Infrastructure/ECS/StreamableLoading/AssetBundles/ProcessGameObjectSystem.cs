using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    [UpdateAfter(typeof(LoadAssetBundleSystem))]
    public partial class ProcessGameObjectSystem : ProcessAssetSystem<GameObject>
    {
        private const float SKINNED_MESH_RENDERER_MAX_BOUNDS = 100;

        public ProcessGameObjectSystem(World world) : base(world)
        {
        }

        protected override void ProcessAsset(GameObject asset)
        {
            _ = ListPool<SkinnedMeshRenderer>.Get(out var skinnedMeshRenderers);
            asset.GetComponentsInChildren(true, skinnedMeshRenderers);

            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
            {
                var bounds = skinnedMeshRenderer.localBounds;
                bounds.extents = Vector3.Min(bounds.extents, SKINNED_MESH_RENDERER_MAX_BOUNDS * Vector3.one);
                skinnedMeshRenderer.localBounds = bounds;
            }
        }
    }
}
