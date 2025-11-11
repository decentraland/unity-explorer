using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class VideoEventsSystem : BaseUnityLoopSystem
    {
        private const float MAX_VIDEO_FROZEN_SECONDS_BEFORE_ERROR = 10f;

        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;

        private VideoEventsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneStateProvider sceneStateProvider, IPerformanceBudget frameTimeBudget) : base(world)
        {
            ecsToCRDTWriter = ecsToCrdtWriter;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
        }

        protected override void Update(float t)
        {
            PropagateVideoEventsQuery(World);
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        private void PropagateVideoEvents(in CRDTEntity sdkEntity, ref MediaPlayerComponent mediaPlayer)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            VideoState state = GetVideoStateForPropagation(mediaPlayer);
            PropagateStateInVideoEvent(in sdkEntity, ref mediaPlayer, state);
        }

        private void PropagateStateInVideoEvent(in CRDTEntity sdkEntity, ref MediaPlayerComponent mediaPlayer, VideoState videoState)
        {
            if (videoState == mediaPlayer.LastPropagatedState
                && mediaPlayer.LastPropagatedVideoTime.Equals(mediaPlayer.CurrentTime)) return;

            mediaPlayer.LastPropagatedState = mediaPlayer.State;
            mediaPlayer.LastPropagatedVideoTime = mediaPlayer.CurrentTime;

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

        private static VideoState GetVideoStateForPropagation(in MediaPlayerComponent mediaPlayer)
        {
            VideoState state = mediaPlayer.State;

            if (state is not (VideoState.VsSeeking or VideoState.VsBuffering)) return state;

            // If the seeking/buffering never ends, update state with error so the scene can react
            if (mediaPlayer.IsFrozen(out float frozenElapsedTime))
                if (frozenElapsedTime > MAX_VIDEO_FROZEN_SECONDS_BEFORE_ERROR)
                    return VideoState.VsError;

            return state;
        }
    }
}
