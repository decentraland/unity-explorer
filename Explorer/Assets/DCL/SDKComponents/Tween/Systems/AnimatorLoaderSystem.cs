using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Unity.Groups;
using System;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.ANIMATOR)]
    [ThrottlingEnabled]
    public partial class AnimatorLoaderSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<SDKAnimatorComponent> sdkAnimatorPool;
        private readonly IComponentPool<SDKAnimationState> sdkAnimationStatePool;

        public AnimatorLoaderSystem(World world, IComponentPool<SDKAnimatorComponent> sdkAnimatorPool, IComponentPool<SDKAnimationState> sdkAnimationStatePool) : base(world)
        {
            this.sdkAnimatorPool = sdkAnimatorPool;
            this.sdkAnimationStatePool = sdkAnimationStatePool;
        }

        protected override void Update(float t)
        {
            UpdateTweenQuery(World);
            LoadTweenQuery(World);
        }

        [Query]
        [None(typeof(AnimatorComponent))]
        private void LoadTween(in Entity entity, ref PBAnimator pbAnimator)
        {
            var animatorComponent = new AnimatorComponent();

            SDKAnimatorComponent sdkAnimatorComponent = sdkAnimatorPool.Get();
            sdkAnimatorComponent.SDKAnimationStates.Clear();

            for (var i = 0; i < pbAnimator.States.Count; i++)
            {
                PBAnimationState pbAnimationState = pbAnimator.States[i];
                var sdkAnimationState = sdkAnimationStatePool.Get();

                sdkAnimationState.Update(
                    pbAnimationState.Clip,
                    pbAnimationState.Playing,
                    pbAnimationState.GetWeight(),
                    pbAnimationState.GetSpeed(),
                    pbAnimationState.GetLoop(),
                    pbAnimationState.GetShouldReset());

                sdkAnimatorComponent.SDKAnimationStates.Add(sdkAnimationState);
            }

            sdkAnimatorComponent.IsDirty = true;
            animatorComponent.SDKAnimatorComponent = sdkAnimatorComponent;

            World.Add(entity, animatorComponent);
        }

        [Query]
        private void UpdateTween(ref PBAnimator pbAnimator, ref AnimatorComponent animatorComponent)
        {
            if (pbAnimator.IsDirty)// || !TweenSDKComponentHelper.AreSameModels(pbAnimator, animatorComponent.SDKTweenComponent.CurrentTweenModel))
            {
                var sdkAnimatorComponent = animatorComponent.SDKAnimatorComponent;
                var sdkAnimationStates = sdkAnimatorComponent.SDKAnimationStates;
                var pbAnimationStates = pbAnimator.States;

                if (pbAnimationStates.Count < sdkAnimationStates.Count)
                {
                    for (int i = pbAnimationStates.Count - 1; i < sdkAnimationStates.Count; i++)
                    {
                        sdkAnimationStatePool.Release(sdkAnimationStates[i]);
                    }
                    sdkAnimationStates.RemoveRange(pbAnimationStates.Count -1, sdkAnimationStates.Count-pbAnimationStates.Count);
                }

                for (var i = 0; i < pbAnimationStates.Count; i++)
                {
                    var pbAnimationState = pbAnimationStates[i];

                    SDKAnimationState sdkAnimationState;

                    if (i > sdkAnimationStates.Count)
                    {
                        sdkAnimationState = sdkAnimationStatePool.Get();
                        sdkAnimationStates.Add(sdkAnimationState);
                    }
                    else { sdkAnimationState = sdkAnimationStates[i]; }

                    sdkAnimationState.Update(
                        pbAnimationState.Clip,
                        pbAnimationState.Playing,
                        pbAnimationState.GetWeight(),
                        pbAnimationState.GetSpeed(),
                        pbAnimationState.GetLoop(),
                        pbAnimationState.GetShouldReset()
                        );
                }

                sdkAnimatorComponent.IsDirty = true;
            }
        }
    }
}
