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
            VideoState newState = GetCurrentVideoState(videoPlayer.MediaPlayer);

            if (videoPlayer.State == newState) return;
            videoPlayer.State = newState;

            using PoolExtensions.Scope<PBVideoEvent> scope = componentPool.AutoScope();
            PBVideoEvent pbVideoEvent = scope.Value
                                             .WithData(in videoPlayer, sceneStateProvider.TickNumber);

            ecsToCRDTWriter.AppendMessage(sdkEntity, pbVideoEvent, (int)pbVideoEvent.Timestamp);
        }

        private static VideoState GetCurrentVideoState(MediaPlayer mediaPlayer)
        {
            if (mediaPlayer.Control.IsPlaying()) return VideoState.VsPlaying;
            if (mediaPlayer.Control.IsPaused()) return VideoState.VsPaused;
            if (mediaPlayer.Control.IsFinished()) return VideoState.VsNone;
            if (mediaPlayer.Control.IsBuffering()) return VideoState.VsBuffering;
            if (mediaPlayer.Control.IsSeeking()) return VideoState.VsSeeking;

            if (mediaPlayer.Control.GetLastError() != ErrorCode.None) return VideoState.VsError;

            return VideoState.VsNone;
        }
    }

    public static class PBVideoEventExtensions
    {
        public static PBVideoEvent WithData(this PBVideoEvent pbVideoEvent, in VideoPlayerComponent videoPlayer, uint tickNumber)
        {
            pbVideoEvent.State = videoPlayer.State;
            pbVideoEvent.CurrentOffset = videoPlayer.CurrentTime;
            pbVideoEvent.VideoLength = videoPlayer.Duration;

            pbVideoEvent.Timestamp = tickNumber;
            pbVideoEvent.TickNumber = tickNumber;

            return pbVideoEvent;
        }
    }
}
