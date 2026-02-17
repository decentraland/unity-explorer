using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.GliderProp;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    public partial class GliderPropControllerSystem : BaseUnityLoopSystem
    {
        private const int PRE_ALLOCATED_PROP_COUNT = 4;

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
            propPool.WarmUp(PRE_ALLOCATED_PROP_COUNT);

            tickEntity = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            int tick = tickEntity.GetPhysicsTickComponent(World).Tick;

            LocalCreatePropQuery(World);
            RemoteCreatePropQuery(World);
            UpdatePropAnimatorQuery(World);
            UpdateTrailQuery(World);
            HandleStateTransitionQuery(World, tick);
            RemoteCleanUpPropQuery(World);
            LocalUpdateEngineStateQuery(World, t);
            RemoteUpdateEngineStateQuery(World, t);
            CleanUpDestroyedAvatarsPropQuery(World);
        }

        [Query]
        [None(typeof(GliderProp))]
        private void LocalCreateProp(Entity entity, in GlideState glideState, in IAvatarView avatarView) =>
            CreateProp(glideState.Value, entity, avatarView);

        [Query]
        [None(typeof(GliderProp), typeof(GlideState))]
        private void RemoteCreateProp(Entity entity, in CharacterAnimationComponent animationComponent, in IAvatarView avatarView) =>
            CreateProp(animationComponent.States.GlideState, entity, avatarView);

        private void CreateProp(GlideStateValue glideState, Entity entity, IAvatarView avatarView)
        {
            // Opening glider or already gliding but prop not spawned yet, spawn it
            if (glideState != GlideStateValue.OPENING_PROP && glideState != GlideStateValue.GLIDING) return;

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
                    break;

                case GlideStateValue.CLOSING_PROP when gliderProp.View.CloseAnimationCompleted:
                    glideState.Value = GlideStateValue.PROP_CLOSED;
                    glideState.CooldownStartedTick = tick;
                    CleanUpProp(entity, gliderProp);
                    break;
            }
        }

        [Query]
        [None(typeof(GlideState))]
        private void RemoteCleanUpProp(Entity entity, in GliderProp gliderProp, in CharacterAnimationComponent animationComponent)
        {
            // Remote players need to be handled separately because they don't have a GlideState component
            if (animationComponent.States.GlideState == GlideStateValue.PROP_CLOSED) CleanUpProp(entity, gliderProp);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanUpDestroyedAvatarsProp(Entity entity, in GliderProp gliderProp) =>
            CleanUpProp(entity, gliderProp);

        private void CleanUpProp(Entity entity, in GliderProp gliderProp)
        {
            World.Remove<GliderProp>(entity);

            ReleasePropAsync(gliderProp.View).Forget();
        }

        private async UniTask ReleasePropAsync(GliderPropView gliderProp)
        {
            // Arbitrary delay to make sure the 'glider closing' sound is fully played (the audio source is attached to the prop)
            await UniTask.Delay(1000);

            propPool!.Release(gliderProp);
            gliderProp.OnReturnedToPool();
        }

        [Query]
        private void LocalUpdateEngineState([Data] float dt, in CharacterAnimationComponent animationComponent, in GliderProp gliderProp, in CharacterRigidTransform rigidTransform) =>
            UpdateEngineState(animationComponent.States.GlideState, gliderProp, rigidTransform.MoveVelocity.Velocity, dt);

        [Query]
        private void RemoteUpdateEngineState([Data] float dt, in CharacterAnimationComponent animationComponent, in GliderProp gliderProp, in InterpolationComponent interpolationComponent) =>
            UpdateEngineState(animationComponent.States.GlideState, gliderProp, interpolationComponent.End.velocity, dt);

        private void UpdateEngineState(in GlideStateValue glideState, in GliderProp gliderProp, in Vector3 velocity, float dt)
        {
            if (glideState != GlideStateValue.GLIDING)
            {
                gliderProp.View.UpdateEngineState(false, 0, dt);
                return;
            }

            // Given it's for audio playback and animation purposes, this is an acceptable approximation that doesn't require sqrt
            float engineLevel = Mathf.Max(Mathf.Abs(velocity.x), Mathf.Abs(velocity.z));
            gliderProp.View.UpdateEngineState(true, engineLevel, dt);
        }
    }
}
