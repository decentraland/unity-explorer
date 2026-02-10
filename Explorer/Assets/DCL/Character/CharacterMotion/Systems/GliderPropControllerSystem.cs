using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using ECS.Abstract;
using ECS.Unity.GliderProp;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GliderPropControllerSystem : BaseUnityLoopSystem
    {
        private readonly CharacterMotionSettings.GlidingSettings glidingSettings;
        private readonly GameObject gliderPrefab;
        private readonly IComponentPoolsRegistry poolsRegistry;
        private SingleInstanceEntity tickEntity;

        private GameObjectPool<GliderPropView>? propPool;

        public GliderPropControllerSystem(World world, CharacterMotionSettings.GlidingSettings glidingSettings, GameObject gliderPrefab, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.glidingSettings = glidingSettings;
            this.gliderPrefab = gliderPrefab;
            this.poolsRegistry = poolsRegistry;
        }

        public override void Initialize()
        {
            propPool = new GameObjectPool<GliderPropView>(poolsRegistry.RootContainerTransform(), () => Object.Instantiate(gliderPrefab).GetComponent<GliderPropView>());
            tickEntity = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            int tick = tickEntity.GetPhysicsTickComponent(World).Tick;

            CreatePropQuery(World);
            UpdatePropAnimatorQuery(World);
            UpdateTrailQuery(World);
            HandleStateTransitionQuery(World, tick);
            RemotePlayerCleanUpPropQuery(World);
            ControlPropAudioQuery(World, t);
        }

        [Query]
        [None(typeof(GliderProp))]
        private void CreateProp(Entity entity, in CharacterAnimationComponent animationComponent, in IAvatarView avatarView)
        {
            // We use the animation component value because that is synced across clients
            if (animationComponent.States.GlideState != GlideStateValue.OPENING_PROP) return;

            var prop = propPool!.Get();
            prop.Animator.Rebind();
            prop.Animator.Update(0);

            var transform = prop.transform;
            transform.SetParent(avatarView.GetTransform(), false);

            World.Add(entity, new GliderProp { View = prop });
        }

        [Query]
        private void UpdatePropAnimator(in GliderProp gliderProp, in CharacterAnimationComponent animationComponent) =>
              GliderPropAnimationLogic.Execute(gliderProp.View.Animator, animationComponent);

        [Query]
        private void UpdateTrail(in GliderProp gliderProp, in CharacterAnimationComponent animationComponent, in CharacterRigidTransform rigidTransform)
        {
            float thresholdSq = glidingSettings.TrailVelocityThreshold * glidingSettings.TrailVelocityThreshold;
            gliderProp.View.TrailEnabled = animationComponent.States.GlideState == GlideStateValue.GLIDING && rigidTransform.MoveVelocity.Velocity.sqrMagnitude > thresholdSq;
        }

        [Query]
        private void HandleStateTransition([Data] int tick, Entity entity, ref GlideState glideState, in GliderProp gliderProp)
        {
            switch (glideState.Value)
            {
                case GlideStateValue.OPENING_PROP when gliderProp.View.OpenAnimationCompleted:
                    glideState.Value = GlideStateValue.GLIDING;
                    gliderProp.View.PlayOpenSound();
                    break;

                case GlideStateValue.CLOSING_PROP when gliderProp.View.CloseAnimationCompleted:
                    glideState.Value = GlideStateValue.PROP_CLOSED;
                    glideState.CooldownStartedTick = tick;
                    CleanUpProp(entity, gliderProp);
                    break;
            }
        }

        [Query]
        [None(typeof(CharacterController))]
        private void RemotePlayerCleanUpProp(Entity entity, in GliderProp gliderProp, in CharacterAnimationComponent animationComponent)
        {
            // Remote players need specific cleanup when their synced glide state reports the glider has been closed
            // For local players we need to time it correctly and do it the same frame the state transition happens
            // (see HandleStateTransition for local player handling)
            if (animationComponent.States.GlideState == GlideStateValue.PROP_CLOSED) CleanUpProp(entity, gliderProp);
        }

        private void CleanUpProp(Entity entity, in GliderProp gliderProp)
        {
            World.Remove<GliderProp>(entity);

            propPool!.Release(gliderProp.View);
            gliderProp.View.OnReturnedToPool();
        }

        [Query]
        private void ControlPropAudio([Data] float dt, in GlideState glideState, in GliderProp gliderProp, in CharacterRigidTransform rigidTransform)
        {
            if (glideState.Value != GlideStateValue.GLIDING)
            {
                gliderProp.View.SetEngineState(false, 0, dt);
                return;
            }

            // Given it's for audio playback purposes, this is an acceptable approximation that doesn't require sqrt
            float engineLevel = Mathf.Max(Mathf.Abs(rigidTransform.MoveVelocity.XVelocity), Mathf.Abs(rigidTransform.MoveVelocity.ZVelocity));
            gliderProp.View.SetEngineState(true, engineLevel, dt);
        }
    }
}
