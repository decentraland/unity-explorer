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
            CleanUpPropQuery(World);
            UpdatePropAnimatorQuery(World);
            UpdateTrailQuery(World);
        }

        [Query]
        [None(typeof(GliderProp))]
        private void CreateProp(Entity entity, in IAvatarView avatarView, in CharacterAnimationComponent animationComponent)
        {
            if (!animationComponent.States.IsGliding) return;

            var prop = propPool!.Get();
            prop.Animator.Rebind();
            prop.Animator.Update(0);

            var transform = prop.transform;
            transform.SetParent(avatarView.GetTransform(), false);

            World.Add(entity, new GliderProp { View = prop });
        }

        [Query]
        private void CleanUpProp(Entity entity, ref GliderProp gliderProp, in CharacterAnimationComponent animationComponent)
        {
            if (animationComponent.States.IsGliding) return;

            World.Remove<GliderProp>(entity);

            propPool!.Release(gliderProp.View);
            gliderProp.View.OnReturnedToPool();
        }

        [Query]
        private void UpdatePropAnimator(in GliderProp gliderProp, in CharacterAnimationComponent animationComponent) =>
            GliderPropAnimationLogic.Execute(gliderProp.View.Animator, animationComponent);

        [Query]
        private void UpdateTrail(in GliderProp gliderProp, in CharacterRigidTransform rigidTransform, in CharacterAnimationComponent animationComponent)
        {
            float thresholdSq = glidingSettings.TrailVelocityThreshold * glidingSettings.TrailVelocityThreshold;
            gliderProp.View.TrailEnabled = animationComponent.States.IsGliding &&
                                           gliderProp.View.OpenAnimationCompleted &&
                                           rigidTransform.MoveVelocity.Velocity.sqrMagnitude > thresholdSq;
        }
    }
}
