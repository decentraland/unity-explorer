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

namespace DCL.SDKComponents.AudioEffectZone.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.VOICE_CHAT)]
    public partial class AudioEffectZoneHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;

        internal AudioEffectZoneHandlerSystem(World world, World globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            UpdateDespatializeZoneQuery(World!);
            UpdateSilenceZoneQuery(World!);

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
