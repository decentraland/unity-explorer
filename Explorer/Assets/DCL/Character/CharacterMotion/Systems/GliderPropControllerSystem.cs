using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GliderPropControllerSystem : BaseUnityLoopSystem
    {
        private readonly GameObject gliderPrefab;
        private readonly IComponentPoolsRegistry poolsRegistry;

        private GameObjectPool<Transform>? propPool;

        public GliderPropControllerSystem(World world, GameObject gliderPrefab, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.gliderPrefab = gliderPrefab;
            this.poolsRegistry = poolsRegistry;
        }

        public override void Initialize() =>
            propPool = new GameObjectPool<Transform>(poolsRegistry.RootContainerTransform(), () => Object.Instantiate(gliderPrefab).transform);

        protected override void Update(float t)
        {
            CreatePropQuery(World);
            AnimatePropQuery(World, t);
        }

        [Query]
        [None(typeof(GliderProp))]
        private void CreateProp(Entity entity, in IAvatarView avatarView, in GlideState glideState)
        {
            if (!glideState.IsGliding) return;

            var gliderProp = propPool!.Get();

            gliderProp.SetParent(avatarView.GetTransform(), false);
            gliderProp.localScale = Vector3.zero;

            World.Add(entity, new GliderProp { Prop = gliderProp });
        }

        [Query]
        private void AnimateProp([Data] float dt, Entity entity, in GlideState glideState, ref GliderProp gliderProp)
        {
            float sign = glideState.IsGliding ? 1 : -1;

            gliderProp.Animation = Mathf.Clamp01(gliderProp.Animation + (sign * 2 * dt));
            float animation = EaseOutBack(gliderProp.Animation);

            gliderProp.Prop.localScale = Vector3.one * animation;

            if (animation <= 0)
            {
                World.Remove<GliderProp>(entity);
                propPool!.Release(gliderProp.Prop);
            }

            float EaseOutBack(float t)
            {
                const float S = 1.70158f;
                t -= 1;
                return 1 + (t * t * (((S + 1) * t) + S));
            }
        }
    }
}
