using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Unity.Groups;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.ANIMATOR)]
    [ThrottlingEnabled]
    public partial class AnimatorLoaderSystem : BaseUnityLoopSystem
    {
        private AnimatorLoaderSystem(World world) : base(world)
        { }

        protected override void Update(float t)
        {
            UpdateTweenQuery(World);
            LoadTweenQuery(World);
            ListPool<int>.Get();
        }

        [Query]
        [None(typeof(SDKAnimatorComponent))]
        private void LoadTween(in Entity entity, ref PBAnimator pbAnimator)
        {
            var sdkAnimationStates = ListPool<SDKAnimationState>.Get();

            for (var i = 0; i < pbAnimator.States.Count; i++)
            {
                PBAnimationState pbAnimationState = pbAnimator.States[i];
                var sdkAnimationState = new SDKAnimationState(pbAnimationState);
                sdkAnimationStates.Add(sdkAnimationState);
            }

            SDKAnimatorComponent sdkAnimatorComponent = new SDKAnimatorComponent(sdkAnimationStates);

            World.Add(entity, sdkAnimatorComponent);
        }

        [Query]
        private void UpdateTween(ref PBAnimator pbAnimator, ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            if (pbAnimator.IsDirty)// || !TweenSDKComponentHelper.AreSameModels(pbAnimator, animatorComponent.SDKTweenComponent.CurrentTweenModel))
            {
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
