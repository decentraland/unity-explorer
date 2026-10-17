// To enable diagnostics, add AUDIO_EVENTS_DEBUG to Player Settings > Scripting Define Symbols
// or uncomment the line below:
// #define AUDIO_EVENTS_DEBUG

using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.MediaStream;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    public partial class AudioEventsSystem : BaseUnityLoopSystem
    {
#if AUDIO_EVENTS_DEBUG
        private static int messagesSent;
        private static int messagesSkipped;
        private static float lastLogTime;
#endif

        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;

        internal AudioEventsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneStateProvider sceneStateProvider, IPerformanceBudget frameTimeBudget) : base(world)
        {
            ecsToCRDTWriter = ecsToCrdtWriter;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
        }

        protected override void Update(float t)
        {
            PropagateAudioSourceEventsQuery(World);
            PropagateAudioStreamEventsQuery(World);

#if AUDIO_EVENTS_DEBUG
            if (UnityEngine.Time.time - lastLogTime > 5f)
            {
                lastLogTime = UnityEngine.Time.time;
                Debug.Log($"[AudioEvents] Last 5s: {messagesSent} CRDT messages sent, {messagesSkipped} skipped (duplicates). Reduction: {(messagesSkipped > 0 ? (100f * messagesSkipped / (messagesSent + messagesSkipped)):0):F1}%");
                messagesSent = 0;
                messagesSkipped = 0;
            }
#endif
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateAudioSourceEvents(in CRDTEntity sdkEntity, ref PBAudioSource sdkComponent, ref AudioSourceComponent audioSourceComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaState state = GetAudioSourceState(in audioSourceComponent);

            // Only propagate if state has changed to avoid CRDT message spam
            if (state == audioSourceComponent.LastPropagatedAudioState)
            {
#if AUDIO_EVENTS_DEBUG
                messagesSkipped++;
#endif
                return;
            }

            MediaState previousState = audioSourceComponent.LastPropagatedAudioState;
            audioSourceComponent.LastPropagatedAudioState = state;
#if AUDIO_EVENTS_DEBUG
            messagesSent++;
#endif
            PropagateStateInAudioEvent(in sdkEntity, state);

            if (IsNaturalFinish(previousState, state, sdkComponent))
                WriteBackNaturalFinish(ecsToCRDTWriter, in sdkEntity, sdkComponent);
        }

        [Query]
        [All(typeof(PBAudioStream))]
        private void PropagateAudioStreamEvents(in CRDTEntity sdkEntity, ref MediaPlayerComponent mediaPlayer)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaState state = GetAudioStreamState(in mediaPlayer);

            // Only propagate if state has changed to avoid CRDT message spam
            if (state == mediaPlayer.LastReportedMediaState)
            {
#if AUDIO_EVENTS_DEBUG
                messagesSkipped++;
#endif
                return;
            }

            mediaPlayer.LastReportedMediaState = state;
#if AUDIO_EVENTS_DEBUG
            messagesSent++;
#endif
            PropagateStateInAudioEvent(in sdkEntity, state);
        }

        /// <summary>
        ///     A natural finish is the playback cursor reaching the end of a non-looping clip: Unity playback stopped
        ///     (MsPlaying -> MsReady) while the scene-authored component still claims Playing == true. Scene-driven
        ///     stops arrive as incoming CRDT PUTs applied at frame start (before this group runs), so they flip
        ///     Playing to false before this check and are never reported as a finish.
        /// </summary>
        internal static bool IsNaturalFinish(MediaState previousState, MediaState newState, PBAudioSource sdkComponent) =>
            previousState == MediaState.MsPlaying
            && newState == MediaState.MsReady
            && sdkComponent is { HasPlaying: true, Playing: true }
            && !(sdkComponent.HasLoop && sdkComponent.Loop);

        internal static void WriteBackNaturalFinish(IECSToCRDTWriter ecsToCRDTWriter, in CRDTEntity sdkEntity, PBAudioSource sdkComponent)
        {
            // Mutate the world instance in place without dirtying it, so UpdateAudioSourceSystem.HandleSDKChanges
            // does not re-apply a Stop to a source that already stopped by itself.
            sdkComponent.Playing = false;

            // PUT a rented copy back to the scene. The pooled message is cleared on Get and released back to the
            // shared pool after serialization, so every field must be copied here and the live entity-attached
            // instance must never be PUT directly (it would land on the pool free-list and alias a future rent).
            ecsToCRDTWriter.PutMessage<PBAudioSource, PBAudioSource>(
                static (dst, src) =>
                {
                    dst.Playing = false;
                    dst.AudioClipUrl = src.AudioClipUrl;

                    if (src.HasVolume) dst.Volume = src.Volume;
                    if (src.HasLoop) dst.Loop = src.Loop;
                    if (src.HasPitch) dst.Pitch = src.Pitch;

                    // CurrentTime is copied verbatim: it is inert while playing == false, and the SDK's playSound()
                    // re-PUTs playing:true which retriggers playback through the existing seek+Play logic.
                    if (src.HasCurrentTime) dst.CurrentTime = src.CurrentTime;
                    if (src.HasGlobal) dst.Global = src.Global;
                },
                sdkEntity, sdkComponent);
        }

        internal static MediaState GetAudioSourceState(in AudioSourceComponent audioSourceComponent)
        {
            // Check if clip is still loading
            if (!audioSourceComponent.ClipPromise.IsConsumed)
                return MediaState.MsLoading;

            // The promise result is retained after consumption, so a failed load is reported as an error
            if (audioSourceComponent.ClipPromise.Result is { Succeeded: false })
                return MediaState.MsError;

            AudioSource? audioSource = audioSourceComponent.AudioSource;

            if (audioSource == null || audioSource.clip == null)
                return !string.IsNullOrEmpty(audioSourceComponent.AudioClipUrl)
                    ? MediaState.MsError // the promise was consumed but produced no clip for the requested URL
                    : MediaState.MsNone;

            // Check if audio is playing, otherwise is ready
            return audioSource.isPlaying ? MediaState.MsPlaying : MediaState.MsReady;
        }

        private MediaState GetAudioStreamState(in MediaPlayerComponent mediaPlayer)
        {
            VideoState videoState = mediaPlayer.State;
            return (MediaState)videoState;
        }

        private void PropagateStateInAudioEvent(in CRDTEntity sdkEntity, MediaState mediaState) =>
            ecsToCRDTWriter.AppendMessage<PBAudioEvent, (MediaState state, uint timestamp)>
            (
                prepareMessage: static (pbAudioEvent, data) =>
                {
                    pbAudioEvent.State = data.state;
                    pbAudioEvent.Timestamp = data.timestamp;
                },
                sdkEntity, (int)sceneStateProvider.TickNumber, (mediaState, sceneStateProvider.TickNumber)
            );
    }
}
