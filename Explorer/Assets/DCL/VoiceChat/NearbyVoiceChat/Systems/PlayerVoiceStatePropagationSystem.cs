using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Mirrors each player's Nearby active-speaker state into the scene the player is assigned to,
    ///     dirtying the scene-world <see cref="PlayerVoiceStateComponent" /> only when the state changes,
    ///     and stops the player speaking in the scene they leave.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerCRDTEntitiesHandlerSystem))]
    [LogCategory(ReportCategory.NEARBY_VOICE_CHAT)]
    public partial class PlayerVoiceStatePropagationSystem : BaseUnityLoopSystem
    {
        private readonly INearbyAudioStreamRegistry registry;

        internal PlayerVoiceStatePropagationSystem(World world, INearbyAudioStreamRegistry registry) : base(world)
        {
            this.registry = registry;
        }

        protected override void Update(float t)
        {
            AddVoiceStateTargetQuery(World);
            PropagateVoiceStateQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PlayerVoiceStateTargetComponent))]
        [All(typeof(Profile), typeof(PlayerCRDTEntity))]
        private void AddVoiceStateTarget(Entity entity)
        {
            World.Add(entity, new PlayerVoiceStateTargetComponent());
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PropagateVoiceState(Profile profile, in PlayerCRDTEntity playerCRDTEntity, ref PlayerVoiceStateTargetComponent target)
        {
            ISceneFacade? scene = playerCRDTEntity.SceneFacade;

            if (target.Scene != scene)
            {
                if (target.Scene != null)
                    StopSpeakingIn(target.Scene, target.SceneEntity);

                target.Scene = scene;
                target.SceneEntity = playerCRDTEntity.SceneWorldEntity;
            }

            if (scene == null) return;

            bool isSpeaking = registry.IsActiveSpeaker(profile.UserId.Value);
            World sceneWorld = scene.EcsExecutor.World;
            Entity sceneEntity = playerCRDTEntity.SceneWorldEntity;
            bool componentFound = sceneWorld.TryGet(sceneEntity, out PlayerVoiceStateComponent voiceState);

            if (componentFound && voiceState.IsSpeaking == isSpeaking) return;

            voiceState.IsSpeaking = isSpeaking;
            voiceState.IsDirty = true;

            if (componentFound)
                sceneWorld.Set(sceneEntity, voiceState);
            else
                sceneWorld.Add(sceneEntity, voiceState);
        }

        private static void StopSpeakingIn(ISceneFacade scene, Entity sceneEntity)
        {
            if (scene.SceneStateProvider.State.Value() is not (SceneState.Running or SceneState.Starting)) return;

            World sceneWorld = scene.EcsExecutor.World;

            if (!sceneWorld.IsAlive(sceneEntity) || sceneWorld.Has<DeleteEntityIntention>(sceneEntity)) return;
            if (!sceneWorld.TryGet(sceneEntity, out PlayerVoiceStateComponent voiceState) || !voiceState.IsSpeaking) return;

            sceneWorld.Set(sceneEntity, new PlayerVoiceStateComponent { IsSpeaking = false, IsDirty = true });
        }
    }
}
