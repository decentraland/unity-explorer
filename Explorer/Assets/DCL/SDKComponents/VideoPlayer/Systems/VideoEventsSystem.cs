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
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [LogCategory(ReportCategory.VIDEO_PLAYER)]
    public partial class VideoEventsSystem: BaseUnityLoopSystem
    {
        private static readonly PBVideoEvent PB_VIDEO_EVENT = new ();

        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<PBVideoEvent> componentPool;

        public VideoEventsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneStateProvider sceneStateProvider, IComponentPool<PBVideoEvent> componentPool) : base(world)
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
            Debug.Log($"VV video event for {sdkEntity}");

            PB_VIDEO_EVENT.State = VideoState.VsPlaying;

            PB_VIDEO_EVENT.Timestamp = sceneStateProvider.TickNumber;
            PB_VIDEO_EVENT.TickNumber = sceneStateProvider.TickNumber;

            PB_VIDEO_EVENT.VideoLength = (float)videoPlayer.MediaPlayer.Info.GetDuration();
            PB_VIDEO_EVENT.CurrentOffset = (float)videoPlayer.MediaPlayer.Control.GetCurrentTime();

            // ecsToCRDTWriter.PutMessage(sdkEntity, pbVideoEvent);
            ecsToCRDTWriter.AppendMessage(sdkEntity, PB_VIDEO_EVENT, (int)PB_VIDEO_EVENT.Timestamp);
        }
    }
}
