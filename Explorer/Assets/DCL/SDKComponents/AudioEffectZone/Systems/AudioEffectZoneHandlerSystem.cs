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
            UpdateAudioEffectZoneQuery(World!);
            SetupAudioEffectZoneQuery(World!);
        }

        public void FinalizeComponents(in Query query) { }

        [Query]
        [None(typeof(SDKEntityTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupAudioEffectZone(in Entity entity, ref PBAudioEffectZone pbAudioEffectZone)
        {
            if (pbAudioEffectZone.EffectCase == PBAudioEffectZone.EffectOneofCase.Silence)
                World!.Add(entity,
                    new SDKEntityTriggerAreaComponent(areaSize: pbAudioEffectZone.Area, targetOnlyMainPlayer: false),
                    new MicrophoneAudioZoneComponent()
                );

                // World!.Add(entity,
                //     new SDKEntityTriggerAreaComponent(areaSize: pbAudioEffectZone.Area, targetOnlyMainPlayer: true),
                //     new SilenceZoneComponent()
                // );
        }

        [Query]
        [All(typeof(TransformComponent), typeof(MicrophoneAudioZoneComponent))]
        private void UpdateAudioEffectZone(ref PBAudioEffectZone pbAudioEffectZone, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            if (pbAudioEffectZone.IsDirty)
                triggerAreaComponent.UpdateAreaSize(pbAudioEffectZone.Area);

            if (triggerAreaComponent.EnteredEntitiesToBeProcessed.Count > 0)
            {
                foreach (Collider? collider in triggerAreaComponent.EnteredEntitiesToBeProcessed)
                {
                    if (!FindAvatarUtils.TryGetAvatarEntity(globalWorld, collider.transform, out Entity entity)) continue;

                    ref ProximityAudioSourceComponent component = ref World.TryGetRef<ProximityAudioSourceComponent>(entity, out bool exists);
                    if (exists)
                    {
                        component.AudioSource.spatialBlend = 0;
                        component.AudioSource.spatialize = false;
                    }
                }

                triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
            }

            if (triggerAreaComponent.ExitedEntitiesToBeProcessed.Count > 0)
            {
                foreach (Collider? collider in triggerAreaComponent.EnteredEntitiesToBeProcessed)
                {
                    if (FindAvatarUtils.TryGetAvatarEntity(globalWorld, collider.transform, out Entity entity))
                    {
                        ref ProximityAudioSourceComponent component = ref World.TryGetRef<ProximityAudioSourceComponent>(entity, out bool exists);
                        if (exists)
                        {
                            component.AudioSource.spatialBlend = 1;
                            component.AudioSource.spatialize = true;
                        }
                    }
                }

                triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
            }
        }

        // [Query]
        // [All(typeof(TransformComponent), typeof(SilenceZoneComponent))]
        // private void UpdateAudioEffectZone(ref PBAudioEffectZone pbAudioEffectZone, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        // {
        //     if (pbAudioEffectZone.IsDirty)
        //         triggerAreaComponent.UpdateAreaSize(pbAudioEffectZone.Area);
        //
        //     if (triggerAreaComponent.EnteredEntitiesToBeProcessed.Count > 0)
        //     {
        //         SetAllProximityAudioMutedQuery(globalWorld, true);
        //         triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
        //     }
        //     else if (triggerAreaComponent.ExitedEntitiesToBeProcessed.Count > 0)
        //     {
        //         SetAllProximityAudioMutedQuery(globalWorld, false);
        //         triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
        //     }
        // }

        [Query]
        private void SetAllProximityAudioMuted([Arch.System.Data] bool muted, ref ProximityAudioSourceComponent proximityAudio)
        {
            if (proximityAudio.AudioSource == null) return;
            proximityAudio.AudioSource.enabled = !muted;
            proximityAudio.AudioSource.mute = muted;

            if(proximityAudio.AudioSourceTransform.TryGetComponent(out LivekitAudioSource lkAudioSource))
                lkAudioSource.enabled = !muted;
        }

    }
}
