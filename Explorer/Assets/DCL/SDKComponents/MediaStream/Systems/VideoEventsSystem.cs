using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
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
        private void PropagateVideoEvents(ref CRDTEntity sdkEntity, ref MediaPlayerComponent mediaPlayer)
        {
            VideoState newState = GetCurrentVideoState(mediaPlayer.MediaPlayer.Control);

            if (mediaPlayer.State == newState) return;
            mediaPlayer.State = newState;

            using PoolExtensions.Scope<PBVideoEvent> scope = componentPool.AutoScope();
            PBVideoEvent pbVideoEvent = scope.Value.WithData(in mediaPlayer, sceneStateProvider.TickNumber);

            ecsToCRDTWriter.AppendMessage(sdkEntity, pbVideoEvent, (int)pbVideoEvent.Timestamp);
        }

        private static VideoState GetCurrentVideoState(IMediaControl mediaPlayerControl)
        {
            if (mediaPlayerControl.IsPlaying()) return VideoState.VsPlaying;
            if (mediaPlayerControl.IsPaused()) return VideoState.VsPaused;
            if (mediaPlayerControl.IsFinished()) return VideoState.VsNone;
            if (mediaPlayerControl.IsBuffering()) return VideoState.VsBuffering;
            if (mediaPlayerControl.IsSeeking()) return VideoState.VsSeeking;

            if (mediaPlayerControl.GetLastError() != ErrorCode.None) return VideoState.VsError;

            return VideoState.VsNone;
        }
    }
}
