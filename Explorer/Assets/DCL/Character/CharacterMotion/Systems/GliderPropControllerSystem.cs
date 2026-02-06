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
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GliderPropControllerSystem : BaseUnityLoopSystem
    {
        private readonly CharacterMotionSettings.GlidingSettings glidingSettings;
        private readonly GameObject gliderPrefab;
        private readonly IComponentPoolsRegistry poolsRegistry;

        private GameObjectPool<GliderPropView>? propPool;

        public GliderPropControllerSystem(World world, CharacterMotionSettings.GlidingSettings glidingSettings, GameObject gliderPrefab, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.glidingSettings = glidingSettings;
            this.gliderPrefab = gliderPrefab;
            this.poolsRegistry = poolsRegistry;
        }

        public override void Initialize() =>
            propPool = new GameObjectPool<GliderPropView>(poolsRegistry.RootContainerTransform(), () => Object.Instantiate(gliderPrefab).GetComponent<GliderPropView>());

        protected override void Update(float t)
        {
            CreatePropQuery(World);
            UpdatePropAnimatorQuery(World);
            UpdateTrailQuery(World);
            HandleStateTransitionQuery(World);
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
        private void HandleStateTransition(Entity entity, ref GlideState glideState, in GliderProp gliderProp)
        {
            switch (glideState.Value)
            {
                case GlideStateValue.OPENING_PROP when gliderProp.View.OpenAnimationCompleted:
                    glideState.Value = GlideStateValue.GLIDING;
                    break;

                case GlideStateValue.CLOSING_PROP when gliderProp.View.CloseAnimationCompleted:
                    glideState.Value = GlideStateValue.PROP_CLOSED;
                    CleanUpProp(entity, gliderProp);
                    break;
            }
        }

        private void CleanUpProp(Entity entity, in GliderProp gliderProp)
        {
            World.Remove<GliderProp>(entity);

            propPool!.Release(gliderProp.View);
            gliderProp.View.OnReturnedToPool();
        }
    }
}
