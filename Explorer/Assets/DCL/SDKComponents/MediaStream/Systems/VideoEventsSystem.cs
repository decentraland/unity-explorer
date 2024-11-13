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
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using UnityEngine;

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

            // The Media Player could already been flagged with errors detected on the video promise, those have to be propagated.
            if (mediaPlayer.State != VideoState.VsError)
            {
                VideoState newState = GetCurrentVideoState(mediaPlayer);

                if (mediaPlayer.State != newState)
                {
                    mediaPlayer.PreviousCurrentTimeChecked = mediaPlayer.MediaPlayer.Control.GetCurrentTime();
                    mediaPlayer.SetState(newState);
                }
            }

            PropagateStateInVideoEvent(in sdkEntity, ref mediaPlayer);
        }

        private void PropagateStateInVideoEvent(in CRDTEntity sdkEntity, ref MediaPlayerComponent mediaPlayer)
        {
            if (mediaPlayer.LastPropagatedState == mediaPlayer.State && mediaPlayer.LastPropagatedVideoTime.Equals(mediaPlayer.CurrentTime)) return;

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

        private static VideoState GetCurrentVideoState(in MediaPlayerComponent mediaPlayer)
        {
            if (string.IsNullOrEmpty(mediaPlayer.URL)) return VideoState.VsNone;

            // Important: while PLAYING or PAUSED, MediaPlayerControl may also be BUFFERING and/or SEEKING.
            var mediaPlayerControl = mediaPlayer.MediaPlayer.Control;

            if (mediaPlayerControl.IsFinished()) return VideoState.VsNone;
            if (mediaPlayerControl.GetLastError() != ErrorCode.None) return VideoState.VsError;
            if (mediaPlayerControl.IsPaused()) return VideoState.VsPaused;

            VideoState state = VideoState.VsNone;
            if (mediaPlayerControl.IsPlaying())
            {
                state = VideoState.VsPlaying;

                if (mediaPlayerControl.GetCurrentTime().Equals(mediaPlayer.PreviousCurrentTimeChecked)) // Video is frozen
                {
                    state = mediaPlayerControl.IsSeeking() ? VideoState.VsSeeking : VideoState.VsBuffering;

                    // If the seeking/buffering never ends, update state with error so the scene can react
                    if ((Time.realtimeSinceStartup - mediaPlayer.LastStateChangeTime) > MAX_VIDEO_FROZEN_SECONDS_BEFORE_ERROR &&
                        mediaPlayer.LastPropagatedState != VideoState.VsPaused)
                    {
                        state = VideoState.VsError;
                    }
                }
            }

            return state;
        }
    }
}
