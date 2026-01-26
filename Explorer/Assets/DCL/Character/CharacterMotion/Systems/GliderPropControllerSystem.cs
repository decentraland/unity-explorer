using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.Optimization.Pools;
using DCL.PluginSystem.Global;
using ECS.Abstract;
using Nethereum.ABI.Util;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GliderPropControllerSystem : BaseUnityLoopSystem
    {
        private readonly CharacterMotionSettings.GlidingSettings glidingSettings;
        private readonly GameObject gliderPrefab;
        private readonly IComponentPoolsRegistry poolsRegistry;

        private GameObjectPool<GliderPropController>? propPool;

        public GliderPropControllerSystem(World world, CharacterMotionSettings.GlidingSettings glidingSettings, GameObject gliderPrefab, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.glidingSettings = glidingSettings;
            this.gliderPrefab = gliderPrefab;
            this.poolsRegistry = poolsRegistry;
        }

        public override void Initialize() =>
            propPool = new GameObjectPool<GliderPropController>(poolsRegistry.RootContainerTransform(), () => Object.Instantiate(gliderPrefab).GetComponent<GliderPropController>());

        protected override void Update(float t)
        {
            CreatePropQuery(World);
            AnimatePropQuery(World, t);
            UpdateTrailQuery(World);
        }

        [Query]
        [None(typeof(GliderProp))]
        private void CreateProp(Entity entity, in IAvatarView avatarView, in CharacterAnimationComponent animationComponent)
        {
            if (!animationComponent.States.IsGliding) return;

            var prop = propPool!.Get();

            var transform = prop.transform;
            transform.SetParent(avatarView.GetTransform(), false);
            transform.localPosition = Vector3.up * 2.2f;
            transform.localRotation = Quaternion.Euler(glidingSettings.StartingRotation, 0, 0);
            transform.localScale = Vector3.zero;

            World.Add(entity, new GliderProp { Controller = prop });
        }

        [Query]
        private void AnimateProp([Data] float dt, Entity entity, in CharacterAnimationComponent animationComponent, ref GliderProp gliderProp)
        {
            var transform = gliderProp.Controller.transform;

            float sign = animationComponent.States.IsGliding ? 1 : -1;

            gliderProp.Animation = Mathf.Clamp01(gliderProp.Animation + (sign * glidingSettings.AnimationSpeed * dt));

            float rotation = glidingSettings.CompleteRotationT > 0
                ? Mathf.Lerp(glidingSettings.StartingRotation, 0, gliderProp.Animation / glidingSettings.CompleteRotationT)
                : 0;
            transform.localRotation = Quaternion.Euler(rotation, 0, 0);

            float scale = sign > 0 ? EaseOutBack(gliderProp.Animation) : EaseInBack(gliderProp.Animation);
            transform.localScale = Vector3.one * scale;

            if (!animationComponent.States.IsGliding && gliderProp.Animation <= 0)
            {
                World.Remove<GliderProp>(entity);
                propPool!.Release(gliderProp.Controller);
            }

            return;

            float EaseOutBack(float t)
            {
                const float S = 1.70158f;
                t -= 1;
                return 1 + (t * t * (((S + 1) * t) + S));
            }

            float EaseInBack(float t)
            {
                const float S = 1.70158f;
                return t * t * (((S + 1) * t) - S);
            }
        }

        [Query]
        private void UpdateTrail(in CharacterRigidTransform rigidTransform, in CharacterAnimationComponent animationComponent, in GliderProp gliderProp)
        {
            float thresholdSq = glidingSettings.TrailVelocityThreshold * glidingSettings.TrailVelocityThreshold;
            gliderProp.Controller.TrailEnabled = animationComponent.States.IsGliding &&
                                                 gliderProp.Animation >= 1 &&
                                                 rigidTransform.MoveVelocity.Velocity.sqrMagnitude > thresholdSq;
        }
    }
}
