using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.AudioEffectZone.Components;
using DCL.SDKEntityTriggerArea.Components;
using DCL.VoiceChat;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;
using LiveKit.Rooms.Streaming.Audio;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DCL.SDKComponents.AudioEffectZone.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.VOICE_CHAT)]
    public partial class AudioEffectZoneHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const string REVERB_ROOM_PARAM = "Reverb_Room";
        private const string REVERB_DECAY_TIME_PARAM = "Reverb_DecayTime";
        private const string REVERB_HF_RATIO_PARAM = "Reverb_HFRatio";
        private const float REVERB_OFF_ROOM = -10000f;

        private readonly World globalWorld;
        private readonly AudioMixerGroup proximityMixerGroup;

        internal AudioEffectZoneHandlerSystem(World world, World globalWorld, AudioMixerGroup proximityMixerGroup) : base(world)
        {
            this.globalWorld = globalWorld;
            this.proximityMixerGroup = proximityMixerGroup;
        }

        protected override void Update(float t)
        {
            UpdateDespatializeZoneQuery(World!);
            UpdateSilenceZoneQuery(World!);
            UpdateAmplifyZoneQuery(World!);
            UpdateReverbZoneQuery(World!);

            SetupAudioEffectZoneQuery(World!);
        }

        public void FinalizeComponents(in Query query) { }

        [Query]
        [None(typeof(SDKEntityTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupAudioEffectZone(in Entity entity, ref PBAudioEffectZone pbAudioEffectZone)
        {
            var triggerArea = new SDKEntityTriggerAreaComponent(areaSize: pbAudioEffectZone.Area, targetOnlyMainPlayer: false);

            switch (pbAudioEffectZone.EffectCase)
            {
                case PBAudioEffectZone.EffectOneofCase.Silence:
                    var silence = pbAudioEffectZone.Silence;
                    World!.Add(entity, triggerArea, new SilenceZoneComponent
                    {
                        ExcludeIds = silence.HasExcludeIds ? silence.ExcludeIds : null,
                    });
                    break;
                case PBAudioEffectZone.EffectOneofCase.Despatialize:
                    World!.Add(entity, triggerArea, new DespatializationAudioZoneComponent());
                    break;
                case PBAudioEffectZone.EffectOneofCase.Amplify:
                    var amplify = pbAudioEffectZone.Amplify;
                    World!.Add(entity, triggerArea, new AmplificationZoneComponent
                    {
                        VolumeMultiplier = amplify.HasVolumeMultiplier ? amplify.VolumeMultiplier : 2.0f,
                        DistanceMultiplier = amplify.HasDistanceMultiplier ? amplify.DistanceMultiplier : 2.0f,
                    });
                    break;
                case PBAudioEffectZone.EffectOneofCase.Reverb:
                    var reverb = pbAudioEffectZone.Reverb;
                    World!.Add(entity, triggerArea, new ReverbAudioZoneComponent
                    {
                        Preset = reverb.HasPreset ? reverb.Preset : PBAudioEffectZone.Types.ReverbPreset.RpRoom,
                    });
                    break;
                case PBAudioEffectZone.EffectOneofCase.Echo:
                    World!.Add(entity, triggerArea, new EchoZoneComponent());
                    break;
            }
        }

        [Query]
        [All(typeof(TransformComponent), typeof(DespatializationAudioZoneComponent))]
        private void UpdateDespatializeZone(ref PBAudioEffectZone pbAudioEffectZone, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            if (pbAudioEffectZone.IsDirty)
            {
                pbAudioEffectZone.IsDirty = false;
                triggerAreaComponent.UpdateAreaSize(pbAudioEffectZone.Area);
            }

            // ENTER - switch to non-spatial 2d Mono
            TrySetSpatialization(isSpatial: false, triggerAreaComponent.EnteredEntitiesToBeProcessed, globalWorld);
            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();

            // EXIT - back to spatial 3d Stereo
            TrySetSpatialization(isSpatial: true, triggerAreaComponent.ExitedEntitiesToBeProcessed, globalWorld);
            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
        }

        [Query]
        [All(typeof(TransformComponent), typeof(AmplificationZoneComponent))]
        private void UpdateAmplifyZone(ref PBAudioEffectZone pbAudioEffectZone, ref SDKEntityTriggerAreaComponent triggerAreaComponent, ref AmplificationZoneComponent amplificationZone)
        {
            if (pbAudioEffectZone.IsDirty)
            {
                pbAudioEffectZone.IsDirty = false;
                triggerAreaComponent.UpdateAreaSize(pbAudioEffectZone.Area);
            }

            TrySetAmplification(amplificationZone.VolumeMultiplier, amplificationZone.DistanceMultiplier, triggerAreaComponent.EnteredEntitiesToBeProcessed, globalWorld);
            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();

            TrySetAmplification(1f / amplificationZone.VolumeMultiplier, 1f / amplificationZone.DistanceMultiplier, triggerAreaComponent.ExitedEntitiesToBeProcessed, globalWorld);
            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
        }

        private static void TrySetAmplification(float volumeMultiplier, float distanceMultiplier, IReadOnlyCollection<Collider> collidersSet, World world)
        {
            foreach (Collider? collider in collidersSet)
            {
                if (!FindAvatarUtils.TryGetAvatarEntity(world, collider.transform, out Entity entity)) return;
                ref ProximityAudioSourceComponent proximityComponent = ref world.TryGetRef<ProximityAudioSourceComponent>(entity, out bool exists);

                if (exists)
                {
                    proximityComponent.AudioSource.volume *= volumeMultiplier;
                    proximityComponent.AudioSource.maxDistance *= distanceMultiplier;
                }
            }
        }

        private static void TrySetSpatialization(bool isSpatial, IReadOnlyCollection<Collider> collidersSet, World world)
        {
            foreach (Collider? collider in collidersSet)
            {
                if (!FindAvatarUtils.TryGetAvatarEntity(world, collider.transform, out Entity entity)) return;
                ref ProximityAudioSourceComponent proximityComponent = ref world.TryGetRef<ProximityAudioSourceComponent>(entity, out bool exists);

                if (exists)
                {
                    proximityComponent.AudioSource.spatialBlend = isSpatial ? 1 : 0;
                    proximityComponent.AudioSource.spatialize = isSpatial;
                }
            }
        }

        [Query]
        [All(typeof(TransformComponent), typeof(ReverbAudioZoneComponent))]
        private void UpdateReverbZone(ref PBAudioEffectZone pbAudioEffectZone, ref SDKEntityTriggerAreaComponent triggerAreaComponent, ref ReverbAudioZoneComponent reverbZone)
        {
            if (pbAudioEffectZone.IsDirty)
            {
                pbAudioEffectZone.IsDirty = false;
                triggerAreaComponent.UpdateAreaSize(pbAudioEffectZone.Area);
            }

            if (triggerAreaComponent.ExitedEntitiesToBeProcessed.Count > 0)
            {
                DisableReverbOnMixer();
                triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
            }
            else if (triggerAreaComponent.EnteredEntitiesToBeProcessed.Count > 0)
            {
                ApplyReverbPresetToMixer(reverbZone.Preset);
                triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
            }
        }

        private void ApplyReverbPresetToMixer(PBAudioEffectZone.Types.ReverbPreset preset)
        {
            if (proximityMixerGroup == null) return;

            AudioMixer mixer = proximityMixerGroup.audioMixer;

            (float room, float decayTime, float hfRatio) = preset switch
            {
                PBAudioEffectZone.Types.ReverbPreset.RpRoom => (-1000f, 0.4f, 0.83f),
                PBAudioEffectZone.Types.ReverbPreset.RpArena => (-698f, 7.24f, 0.33f),
                PBAudioEffectZone.Types.ReverbPreset.RpCave => (-602f, 2.91f, 1.3f),
                PBAudioEffectZone.Types.ReverbPreset.RpAuditotrium => (-1100f, 4.32f, 0.59f),
                _ => (-1000f, 0.4f, 0.83f),
            };

            mixer.SetFloat(REVERB_ROOM_PARAM, room);
            mixer.SetFloat(REVERB_DECAY_TIME_PARAM, decayTime);
            mixer.SetFloat(REVERB_HF_RATIO_PARAM, hfRatio);
        }

        private void DisableReverbOnMixer()
        {
            if (proximityMixerGroup == null) return;

            proximityMixerGroup.audioMixer.SetFloat(REVERB_ROOM_PARAM, REVERB_OFF_ROOM);
        }

        [Query]
        [All(typeof(TransformComponent), typeof(SilenceZoneComponent))]
        private void UpdateSilenceZone(ref PBAudioEffectZone pbAudioEffectZone, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            if (pbAudioEffectZone.IsDirty)
                triggerAreaComponent.UpdateAreaSize(pbAudioEffectZone.Area);

            // EXIT PRIORITY
            if (triggerAreaComponent.ExitedEntitiesToBeProcessed.Count > 0)
            {
                SetAllProximityAudioMutedQuery(globalWorld, false);
                triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
            }
            else if (triggerAreaComponent.EnteredEntitiesToBeProcessed.Count > 0)
            {
                SetAllProximityAudioMutedQuery(globalWorld, true);
                triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
            }
        }

        [Query]
        private void SetAllProximityAudioMuted([Data] bool muted, ref ProximityAudioSourceComponent proximityAudio)
        {
            proximityAudio.AudioSource.enabled = !muted;
            proximityAudio.AudioSource.mute = muted;

            if(proximityAudio.AudioSourceTransform.TryGetComponent(out LivekitAudioSource lkAudioSource))
                lkAudioSource.enabled = !muted;
        }
    }
}
