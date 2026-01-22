using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Unity.GLTFContainer;
using SceneRunner.Scene;

namespace ECS.Unity.AssetLoad.Systems
{
    /// <summary>
    ///     Processes the asset loading updates and sends them to CRDT avoiding sending multiple updates per tick for the same asset.
    ///     This can happen when an asset is already cached and loading progresses very fast.
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(FinalizeAssetPreLoadSystem))]
    public partial class HandleAssetPreloadUpdates : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly AssetPreLoadUtils assetPreLoadUtils;

        internal HandleAssetPreloadUpdates(World world,
            IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider,
            AssetPreLoadUtils assetPreLoadUtils)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
            this.assetPreLoadUtils = assetPreLoadUtils;
        }

        protected override void Update(float t)
        {
            if (assetPreLoadUtils.assetLoadingUpdates.Count == 0)
                return;

            int tick = (int)sceneStateProvider.TickNumber;

            foreach (var kvp in assetPreLoadUtils.assetLoadingUpdates)
            {
                if (kvp.Value.LastTick >= tick || kvp.Value.States.Count == 0)
                    continue;

                SendUpdate(kvp.Value.CrdtEntity, kvp.Value.States[0], kvp.Key, tick);

                kvp.Value.States.RemoveAt(0);
                kvp.Value.LastTick = tick;
            }

        }

        private void SendUpdate(CRDTEntity crdtEntity, LoadingState loadingState, string assetPath, int tick)
        {
            ecsToCRDTWriter.AppendMessage<PBAssetLoadLoadingState, (LoadingState loadingState, string assetPath, uint timestamp)>(
                static (component, data) =>
                {
                    component.CurrentState = data.loadingState;
                    component.Asset = data.assetPath;
                    component.Timestamp = data.timestamp;
                },
                crdtEntity,
                tick,
                (loadingState, assetPath, sceneStateProvider.TickNumber)
            );
        }
    }
}
