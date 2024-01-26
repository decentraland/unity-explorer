using ECS.Abstract;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Groups;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class VideoEventsSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<PBVideoEvent> componentPool;

        private VideoEventsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneStateProvider sceneStateProvider, IComponentPool<PBVideoEvent> componentPool) : base(world)
        {
            ecsToCRDTWriter = ecsToCrdtWriter;
            this.sceneStateProvider = sceneStateProvider;
            this.componentPool = componentPool;
        }

        protected override void Update(float t)
        {
            PropagateVideoEventsQuery(World);
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        private void PropagateVideoEvents(ref CRDTEntity sdkEntity, ref VideoPlayerComponent videoPlayer)
        {
            if (!videoPlayer.StateHasChanged()) return;

            using PoolExtensions.Scope<PBVideoEvent> scope = componentPool.AutoScope();
            PBVideoEvent pbVideoEvent = scope.Value;

            pbVideoEvent.State = videoPlayer.CurrentState;
            pbVideoEvent.CurrentOffset = videoPlayer.CurrentTime;
            pbVideoEvent.VideoLength = videoPlayer.Duration;

            pbVideoEvent.Timestamp = sceneStateProvider.TickNumber;
            pbVideoEvent.TickNumber = sceneStateProvider.TickNumber;

            ecsToCRDTWriter.AppendMessage(sdkEntity, pbVideoEvent, (int)pbVideoEvent.Timestamp);
        }
    }
}
