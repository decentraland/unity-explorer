using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Threading;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.Ipfs.SceneAssetBundleManifest, ECS.StreamableLoading.AssetBundles.GetAssetBundleManifestIntention>;

namespace ECS.SceneLifeCycle.Systems
{
    //TODO (JUANI): WHERE SHOULD THIS CLASS GO? PLUGINS OR ASSET BUNDLES?
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveStaticPointersSystem))]
    public partial class ResolveSceneAssetBundleManifest : BaseUnityLoopSystem
    {
        public ResolveSceneAssetBundleManifest(World world) : base(world) { }

        protected override void Update(float t)
        {
            StartAssetBundleManifestPromiseQuery(World);
            ResolveAssetBundleManifestPromiseQuery(World);
        }

        [Query]
        [None(typeof(AssetBundleManifestPromise))]
        public void StartAssetBundleManifestPromise(Entity entity, in SceneDefinitionComponent sceneDefinition, ref PartitionComponent partitionComponent)
        {
            var promise = AssetBundleManifestPromise.Create(World,
                GetAssetBundleManifestIntention.Create(sceneDefinition.Definition.id, new CommonLoadingArguments(sceneDefinition.Definition.id, cancellationTokenSource: new CancellationTokenSource())),
                partitionComponent);

            World.Add(entity, promise);
        }

        [Query]
        public void ResolveAssetBundleManifestPromise(Entity entity, ref SceneDefinitionComponent sceneDefinition, ref AssetBundleManifestPromise promise)
        {
            if (promise.IsConsumed)
                return;

            if (promise.TryConsume(World, out StreamableLoadingResult<SceneAssetBundleManifest> sceneAssetBundleManifestResult))
            {
                sceneDefinition.Definition.AssetBundleManifest = sceneAssetBundleManifestResult.Asset!;
                World.Add(entity, sceneAssetBundleManifestResult.Asset);
            }
        }
    }
}
