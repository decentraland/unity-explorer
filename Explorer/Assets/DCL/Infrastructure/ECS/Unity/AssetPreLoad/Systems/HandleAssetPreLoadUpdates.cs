using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.AssetLoad.Components;
using SceneRunner.Scene;

namespace ECS.Unity.AssetLoad.Systems
{
    /// <summary>
    ///     Processes the asset loading updates and sends them to CRDT avoiding sending multiple updates per tick for the same asset.
    ///     This can happen when an asset is already cached and loading progresses very fast.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(AssetPreLoadSystem))]
    [ThrottlingEnabled]
    public partial class HandleAssetPreLoadUpdates : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;

        internal HandleAssetPreLoadUpdates(World world,
            IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            SendUpdateQuery(World);
        }

        [Query]
        private void SendUpdate(ref AssetPreLoadLoadingStateComponent loadingStateComponent)
        {
            if (!loadingStateComponent.IsDirty) return;

            int tick = (int)sceneStateProvider.TickNumber;

            if (tick <= loadingStateComponent.LastUpdatedTick) return;

            SendUpdate(loadingStateComponent.MainCRDTEntity, loadingStateComponent.LoadingState, loadingStateComponent.AssetPath, tick);

            loadingStateComponent.LastUpdatedTick = tick;
            loadingStateComponent.IsDirty = false;
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
