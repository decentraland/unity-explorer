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
        private void PropagateAudioSourceEvents(in CRDTEntity sdkEntity, in AudioSourceComponent audioSourceComponent)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaState state = GetAudioSourceState(in audioSourceComponent);
            PropagateStateInAudioEvent(in sdkEntity, state);
        }

        [Query]
        [All(typeof(MediaPlayerComponent))]
        private void PropagateAudioStreamEvents(in CRDTEntity sdkEntity, in MediaPlayerComponent mediaPlayer)
        {
            if (!frameTimeBudget.TrySpendBudget()) return;

            MediaState state = GetAudioStreamState(in mediaPlayer);
            PropagateStateInAudioEvent(in sdkEntity, state);
        }

        private static MediaState GetAudioSourceState(in AudioSourceComponent audioSourceComponent)
        {
            // Check if clip is still loading
            if (!audioSourceComponent.ClipPromise.IsConsumed)
                return MediaState.MsLoading;

            AudioSource? audioSource = audioSourceComponent.AudioSource;

            // If we have a URL but no clip yet, it might be loading or failed
            if (audioSource == null || audioSource.clip == null)
                return !string.IsNullOrEmpty(audioSourceComponent.AudioClipUrl)
                    ? MediaState.MsLoading
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
