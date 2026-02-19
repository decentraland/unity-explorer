using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement;
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
        private readonly CharacterMotionSettings.GlidingSettings glidingSettings;
        private readonly GameObject gliderPrefab;
        private SingleInstanceEntity tickEntity;

        public GliderPropControllerSystem(World world, CharacterMotionSettings.GlidingSettings glidingSettings, GameObject gliderPrefab) : base(world)
        {
            this.glidingSettings = glidingSettings;
            this.gliderPrefab = gliderPrefab;
        }

        public override void Initialize() =>
            tickEntity = World.CachePhysicsTick();

        protected override void Update(float t)
        {
            int tick = tickEntity.GetPhysicsTickComponent(World).Tick;

            // One time init
            CreatePropQuery(World);

            // Lifecycle
            EnablePropQuery(World);
            HandleStateTransitionQuery(World, tick);
            DisablePropQuery(World);

            // Visualization
            UpdatePropAnimatorQuery(World);
            LocalUpdateTrailQuery(World);
            RemoteUpdateTrailQuery(World);
            LocalUpdateEngineStateQuery(World, t);
            RemoteUpdateEngineStateQuery(World, t);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(GliderProp))]
        private void CreateProp(Entity entity, IAvatarView avatarView)
        {
            var prop = Object.Instantiate(gliderPrefab).GetComponent<GliderPropView>();
            prop.gameObject.SetActive(false);

            var transform = prop.transform;
            transform.SetParent(avatarView.GetTransform(), false);

            World.Add(entity, new GliderProp { View = prop });
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(GliderPropEnabled))]
        private void EnableProp(Entity entity, in GliderProp gliderProp, in CharacterAnimationComponent animationComponent)
        {
            if (animationComponent.States.GlideState == GlideStateValue.PROP_CLOSED) return;

            gliderProp.View.gameObject.SetActive(true);
            World.Add<GliderPropEnabled>(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void DisableProp(Entity entity, in GliderProp gliderProp, in CharacterAnimationComponent animationComponent)
        {
            if (animationComponent.States.GlideState != GlideStateValue.PROP_CLOSED) return;

            World.Remove<GliderPropEnabled>(entity);
            gliderProp.View.PrepareForNextActivation();

            DisablePropDelayedAsync(entity, gliderProp.View).Forget();
        }

        private async UniTask DisablePropDelayedAsync(Entity entity, GliderPropView view)
        {
            // Arbitrary delay to make sure the 'glider closing' sound is fully played (the audio source is attached to the prop)
            await UniTask.Delay(500);

            if (!World.IsAlive(entity) || World.Has<GliderPropEnabled>(entity)) return;

            view.gameObject.SetActive(false);
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
        private void UpdatePropAnimator(in GliderProp gliderProp, in CharacterAnimationComponent animationComponent) =>
              GliderPropAnimationLogic.Execute(gliderProp.View.Animator, animationComponent);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void LocalUpdateTrail(in GliderProp gliderProp, in GlideState glideState, in CharacterRigidTransform rigidTransform) =>
            UpdateTrail(glideState.Value, gliderProp, rigidTransform.MoveVelocity.Velocity);

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(GliderPropEnabled))]
        private void RemoteUpdateTrail(in GliderProp gliderProp, in CharacterAnimationComponent animationComponent, in InterpolationComponent interpolationComponent) =>
            UpdateTrail(animationComponent.States.GlideState, gliderProp, interpolationComponent.End.velocity);

        private void UpdateTrail(in GlideStateValue glideState, in GliderProp gliderProp, in Vector3 velocity)
        {
            float thresholdSq = glidingSettings.TrailVelocityThreshold * glidingSettings.TrailVelocityThreshold;
            gliderProp.View.TrailEnabled = glideState == GlideStateValue.GLIDING && velocity.sqrMagnitude > thresholdSq;
        }

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
