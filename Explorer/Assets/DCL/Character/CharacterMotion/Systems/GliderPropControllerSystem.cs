using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
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
        private readonly GliderPropView gliderPrefab;
        private readonly IComponentPoolsRegistry poolsRegistry;

        private GameObjectPool<GliderPropView>? propPool;
        private GameObjectPool<OneShotAudioSource> oneShotAudioPool;
        private SingleInstanceEntity tickEntity;

        public GliderPropControllerSystem(World world, CharacterMotionSettings.GlidingSettings glidingSettings, GliderPropView gliderPrefab, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.glidingSettings = glidingSettings;
            this.gliderPrefab = gliderPrefab;
            this.poolsRegistry = poolsRegistry;
        }

        public override void Initialize()
        {
            // When playing in editor we want to be able to toggle pooling while in play mode so we always create the pool
#if !UNITY_EDITOR
            if (glidingSettings.EnablePropPooling)
            {
#endif
                propPool = new GameObjectPool<GliderPropView>(poolsRegistry.RootContainerTransform(), () => Object.Instantiate(gliderPrefab));
                propPool.WarmUp(PRE_ALLOCATED_PROP_COUNT);
#if !UNITY_EDITOR
            }
#endif

            Transform poolRoot = poolsRegistry.RootContainerTransform();
            oneShotAudioPool = new GameObjectPool<OneShotAudioSource>(poolRoot, () =>
            {
                var go = new GameObject("GliderOneShotAudio");
                var oneShot = go.AddComponent<AudioSource>();
                var component = go.AddComponent<OneShotAudioSource>();
                component.Initialize(oneShotAudioPool);
                return component;
            });

            tickEntity = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            int tick = tickEntity.GetPhysicsTickComponent(World).Tick;

            // One time init
            LocalCreatePropQuery(World);
            RemoteCreatePropQuery(World);

            // Lifecycle
            LocalDisablePropQuery(World);
            RemoteDisablePropQuery(World);
            LocalEnablePropQuery(World);
            RemoteEnablePropQuery(World);
            HandleStateTransitionQuery(World, tick);
            CleanUpDestroyedAvatarsPropQuery(World);

            // Visualization
            LocalUpdatePropAnimatorQuery(World);
            RemoteUpdatePropAnimatorQuery(World);
            LocalUpdateTrailQuery(World);
            RemoteUpdateTrailQuery(World);
            LocalUpdateEngineStateQuery(World, t);
            RemoteUpdateEngineStateQuery(World, t);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention), typeof(GliderProp))]
        private void LocalCreateProp(Entity entity, IAvatarView avatarView) =>
            CreateProp(entity, avatarView, true);

        [Query]
        [All(typeof(InterpolationComponent))]
        [None(typeof(DeleteEntityIntention), typeof(GliderProp))]
        private void RemoteCreateProp(Entity entity, IAvatarView avatarView) =>
            CreateProp(entity, avatarView, false);

        private void CreateProp(Entity entity, IAvatarView avatarView, bool isLocalPlayer)
        {
            var prop = glidingSettings.EnablePropPooling
                ? propPool!.Get()
                : Object.Instantiate(gliderPrefab);
            prop.PlayOpenAndCloseSounds = isLocalPlayer;
            prop.OneShotAudioPool = oneShotAudioPool;
            prop.gameObject.SetActive(false);

            var transform = prop.transform;
            transform.SetParent(avatarView.GetTransform(), false);

            World.Add(entity, new GliderProp { View = prop });
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(GliderPropEnabled))]
        private void LocalEnableProp(Entity entity, in GliderProp gliderProp, in GlideState glideState) =>
            EnableProp(entity, gliderProp, glideState.Value);

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(GliderPropEnabled))]
        private void RemoteEnableProp(Entity entity, in GliderProp gliderProp, in InterpolationComponent interpolationComponent) =>
            EnableProp(entity, gliderProp, interpolationComponent.End.animState.GlideState);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void LocalDisableProp(Entity entity, in GliderProp gliderProp, in GlideState glideState) =>
            DisableProp(entity, gliderProp, glideState.Value);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void RemoteDisableProp(Entity entity, in GliderProp gliderProp, in InterpolationComponent interpolationComponent) =>
            DisableProp(entity, gliderProp, interpolationComponent.End.animState.GlideState);

        private void EnableProp(Entity entity, in GliderProp gliderProp, GlideStateValue glideState)
        {
            if (glideState == GlideStateValue.PROP_CLOSED) return;

            gliderProp.View.gameObject.SetActive(true);
            World.Add<GliderPropEnabled>(entity);
        }

        private void DisableProp(Entity entity, in GliderProp gliderProp, GlideStateValue glideState)
        {
            if (glideState != GlideStateValue.PROP_CLOSED) return;

            gliderProp.View.PrepareForNextActivation();
            gliderProp.View.gameObject.SetActive(false);

            World.Remove<GliderPropEnabled>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanUpDestroyedAvatarsProp(ref GliderProp gliderProp)
        {
            if (glidingSettings.EnablePropPooling)
            {
                gliderProp.View.PrepareForNextActivation();
                propPool!.Release(gliderProp.View);
            }
            else
            {
                Object.Destroy(gliderProp.View.gameObject);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void HandleStateTransition([Data] int tick, ref GlideState glideState, in GliderProp gliderProp)
        {
            switch (glideState.Value)
            {
                case GlideStateValue.OPENING_PROP when gliderProp.View.OpenAnimationCompleted:
                    glideState.Value = GlideStateValue.GLIDING;
                    break;

                case GlideStateValue.CLOSING_PROP when gliderProp.View.CloseAnimationCompleted:
                    glideState.Value = GlideStateValue.PROP_CLOSED;
                    glideState.CooldownStartedTick = tick;
                    break;
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void LocalUpdatePropAnimator(in GliderProp gliderProp, in CharacterAnimationComponent animationComponent, in GlideState glideState) =>
              GliderPropAnimationLogic.Execute(gliderProp.View.Animator, animationComponent, glideState.Value);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void RemoteUpdatePropAnimator(in GliderProp gliderProp, in CharacterAnimationComponent animationComponent, in InterpolationComponent interpolationComponent) =>
            GliderPropAnimationLogic.Execute(gliderProp.View.Animator, animationComponent, interpolationComponent.End.animState.GlideState);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void LocalUpdateTrail(in GliderProp gliderProp, in GlideState glideState, in CharacterRigidTransform rigidTransform)
        {
            float thresholdSq = glidingSettings.TrailVelocityThreshold * glidingSettings.TrailVelocityThreshold;
            Vector3 velocity = rigidTransform.MoveVelocity.Velocity;
            gliderProp.View.TrailEnabled = glideState.Value == GlideStateValue.GLIDING && velocity.sqrMagnitude > thresholdSq;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled), typeof(InterpolationComponent))]
        private void RemoteUpdateTrail(in GliderProp gliderProp) =>
            gliderProp.View.TrailEnabled = false;

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void LocalUpdateEngineState([Data] float dt, in GlideState glideState, in GliderProp gliderProp, in CharacterRigidTransform rigidTransform) =>
            UpdateEngineState(glideState.Value, gliderProp, rigidTransform.MoveVelocity.Velocity, dt);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
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
