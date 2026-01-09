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
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    public partial class AudioEventsSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IPerformanceBudget frameTimeBudget;

        private AudioEventsSystem(World world, IECSToCRDTWriter ecsToCrdtWriter, ISceneStateProvider sceneStateProvider, IPerformanceBudget frameTimeBudget) : base(world)
        {
            ecsToCRDTWriter = ecsToCrdtWriter;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudget = frameTimeBudget;
        }

        protected override void Update(float t)
        {
            PropagateAudioSourceEventsQuery(World);
            PropagateAudioStreamEventsQuery(World);
        }

        [Query]
        [All(typeof(AudioSourceComponent))]
        private void PropagateAudioSourceEvents(in CRDTEntity sdkEntity, ref AudioSourceComponent audioSourceComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaState state = GetAudioSourceState(ref audioSourceComponent);
            PropagateStateInAudioEvent(in sdkEntity, state);
        }

        [Query]
        [All(typeof(MediaPlayerComponent))]
        private void PropagateAudioStreamEvents(in CRDTEntity sdkEntity, ref MediaPlayerComponent mediaPlayer)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaState state = GetAudioStreamState(ref mediaPlayer);
            PropagateStateInAudioEvent(in sdkEntity, state);
        }

        // TODO MAURIZIO check with Pravus if this logic is correct
        private static MediaState GetAudioSourceState(ref AudioSourceComponent audioSourceComponent)
        {
            // Check if clip is still loading
            if (!audioSourceComponent.ClipPromise.IsConsumed)
                return MediaState.MsLoading;

            // Check if AudioSource is assigned and has a clip
            AudioSource? audioSource = audioSourceComponent.AudioSource;

            // If we have a URL but no clip yet, it might be loading or failed
            if (audioSource == null || audioSource.clip == null)
                return !string.IsNullOrEmpty(audioSourceComponent.AudioClipUrl)
                    ? MediaState.MsLoading
                    : MediaState.MsNone;

            // Check if audio is playing, otherwise is ready
            return audioSource.isPlaying ? MediaState.MsPlaying : MediaState.MsReady;
        }

        private MediaState GetAudioStreamState(ref MediaPlayerComponent mediaPlayer)
        {
            // Convert VideoState to MediaState (they have the same numeric values)
            VideoState videoState = mediaPlayer.State;
            return (MediaState)videoState;
        }

        private void PropagateStateInAudioEvent(in CRDTEntity sdkEntity, MediaState mediaState) =>

            // Always update the PBAudioEvent component with the current state
            // The CRDT system will handle deduplication based on timestamp
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
