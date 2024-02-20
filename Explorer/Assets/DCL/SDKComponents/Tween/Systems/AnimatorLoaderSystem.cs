using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Unity.Groups;
using Google.Protobuf.Collections;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Tween.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.ANIMATOR)]
    [ThrottlingEnabled]
    public partial class AnimatorLoaderSystem : BaseUnityLoopSystem
    {
        private AnimatorLoaderSystem(World world) : base(world) { }

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
            List<SDKAnimationState> sdkAnimationStates = ListPool<SDKAnimationState>.Get();

            for (var i = 0; i < pbAnimator.States.Count; i++)
            {
                PBAnimationState pbAnimationState = pbAnimator.States[i];
                var sdkAnimationState = new SDKAnimationState(pbAnimationState);
                sdkAnimationStates.Add(sdkAnimationState);
            }

            if (pbAnimator.IsDirty)
            {
                var a = 1;
            }

            var sdkAnimatorComponent = new SDKAnimatorComponent(sdkAnimationStates);

            World.Add(entity, sdkAnimatorComponent);
        }

        [Query]
        private void UpdateTween(ref PBAnimator pbAnimator, ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            if (!pbAnimator.IsDirty) return;

            // || Check if models are different? not sure if its needed, as new models should be dirty always

            sdkAnimatorComponent.IsDirty = true;
            List<SDKAnimationState> sdkAnimationStates = sdkAnimatorComponent.SDKAnimationStates;
            sdkAnimationStates.Clear();

            RepeatedField<PBAnimationState> pbAnimatorStates = pbAnimator.States;

            for (var i = 0; i < pbAnimatorStates.Count; i++)
            {
                var sdkAnimationState = new SDKAnimationState(pbAnimatorStates[i]);
                sdkAnimationStates.Add(sdkAnimationState);
            }
        }
    }
}
