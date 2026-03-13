using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.AudioEffectZone.Components;
using DCL.SDKEntityTriggerArea.Components;
using DCL.VoiceChat;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
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

            HandleEntityDestructionQuery(World!);
            HandleComponentRemovalQuery(World!);
        }

        public void FinalizeComponents(in Query query)
        {
            ResetAffectedEntitiesQuery(World!);
        }

        [Query]
        [All(typeof(AudioEffectZoneComponent))]
        private void ResetAffectedEntities(in Entity entity, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            UnmuteAll(ref triggerAreaComponent);
            World!.Remove<AudioEffectZoneComponent>(entity);
        }

        [Query]
        [None(typeof(SDKEntityTriggerAreaComponent), typeof(AudioEffectZoneComponent))]
        [All(typeof(TransformComponent))]
        private void SetupAudioEffectZone(in Entity entity, ref PBAudioEffectZone pbAudioEffectZone)
        {
            World!.Add(entity,
                new SDKEntityTriggerAreaComponent(areaSize: pbAudioEffectZone.Area, targetOnlyMainPlayer: false),
                new AudioEffectZoneComponent()
            );
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateAudioEffectZone(ref PBAudioEffectZone pbAudioEffectZone,
            ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            if (pbAudioEffectZone.IsDirty)
            {
                pbAudioEffectZone.IsDirty = false;
                triggerAreaComponent.UpdateAreaSize(pbAudioEffectZone.Area);
            }

            foreach (Collider avatarCollider in triggerAreaComponent.ExitedEntitiesToBeProcessed)
            {
                if (!TryGetProximityAudioSource(avatarCollider.transform, out ProximityAudioSourceComponent proximityAudio))
                    continue;

                if (proximityAudio.AudioSource != null)
                    proximityAudio.AudioSource.mute = false;
            }

            triggerAreaComponent.TryClearExitedAvatarsToBeProcessed();

            if (pbAudioEffectZone.EffectCase == PBAudioEffectZone.EffectOneofCase.Silence)
            {
                foreach (Collider avatarCollider in triggerAreaComponent.EnteredEntitiesToBeProcessed)
                {
                    if (!TryGetProximityAudioSource(avatarCollider.transform, out ProximityAudioSourceComponent proximityAudio))
                        continue;

                    if (proximityAudio.AudioSource != null)
                        proximityAudio.AudioSource.mute = true;
                }
            }

            triggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBAudioEffectZone))]
        private void HandleEntityDestruction(ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            UnmuteAll(ref triggerAreaComponent);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBAudioEffectZone))]
        private void HandleComponentRemoval(in Entity entity, ref SDKEntityTriggerAreaComponent triggerAreaComponent,
            ref AudioEffectZoneComponent _)
        {
            UnmuteAll(ref triggerAreaComponent);
            World!.Remove<AudioEffectZoneComponent>(entity);
        }

        private void UnmuteAll(ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            foreach (Collider avatarCollider in triggerAreaComponent.CurrentEntitiesInside)
            {
                if (!TryGetProximityAudioSource(avatarCollider.transform, out ProximityAudioSourceComponent proximityAudio))
                    continue;

                if (proximityAudio.AudioSource != null)
                    proximityAudio.AudioSource.mute = false;
            }
        }

        private bool TryGetProximityAudioSource(Transform transform, out ProximityAudioSourceComponent proximityAudio)
        {
            proximityAudio = default;
            var result = FindAvatarUtils.AvatarWithTransform(globalWorld, transform);
            if (!result.Success) return false;
            return globalWorld.TryGet(result.Result, out proximityAudio);
        }
    }
}
