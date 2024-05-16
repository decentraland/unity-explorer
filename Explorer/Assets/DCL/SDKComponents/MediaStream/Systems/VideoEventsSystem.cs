using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class VideoEventsSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IComponentPool<PBVideoEvent> componentPool;
        private readonly IPerformanceBudget frameTimeBudget;

        private VideoEventsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneStateProvider sceneStateProvider, IComponentPool<PBVideoEvent> componentPool, IPerformanceBudget frameTimeBudget) : base(world)
        {
            ecsToCRDTWriter = ecsToCrdtWriter;
            this.sceneStateProvider = sceneStateProvider;
            this.componentPool = componentPool;
            this.frameTimeBudget = frameTimeBudget;
        }

        protected override void Update(float t)
        {
            PropagateVideoEventsQuery(World);
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        private void PropagateVideoEvents(ref CRDTEntity sdkEntity, ref MediaPlayerComponent mediaPlayer)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            VideoState newState = GetCurrentVideoState(mediaPlayer.MediaPlayer.Control);

            if (mediaPlayer.State == newState) return;
            mediaPlayer.State = newState;

            AppendMessage(in sdkEntity, in mediaPlayer);
        }

        private void AppendMessage(in CRDTEntity sdkEntity, in MediaPlayerComponent mediaPlayer)
        {
            ecsToCRDTWriter.AppendMessage<PBVideoEvent, (MediaPlayerComponent mediaPlayer, uint timestamp)>
            (
                prepareMessage: static (pbVideoEvent, data) =>
                {
                    pbVideoEvent.State = data.mediaPlayer.State;
                    pbVideoEvent.CurrentOffset = data.mediaPlayer.CurrentTime;
                    pbVideoEvent.VideoLength = data.mediaPlayer.Duration;

                    pbVideoEvent.Timestamp = data.timestamp;
                    pbVideoEvent.TickNumber = data.timestamp;
                },
                sdkEntity, (int)sceneStateProvider.TickNumber, (mediaPlayer, sceneStateProvider.TickNumber)
            );
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
