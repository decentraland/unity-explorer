using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Animator.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using Google.Protobuf.Collections;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Animator.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.ANIMATOR)]
    [ThrottlingEnabled]
    public partial class SDKAnimatorUpdaterSystem : BaseUnityLoopSystem
    {
        public SDKAnimatorUpdaterSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateAnimatorQuery(World);
            HandleEntityDeletionQuery(World);
            World.Remove<SDKAnimatorComponent>(in HandleEntityDeletion_QueryDescription);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDeletion(ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            ListPool<SDKAnimationState>.Release(sdkAnimatorComponent.SDKAnimationStates);
        }

        [Query]
        private void UpdateAnimator(ref PBAnimator pbAnimator, ref SDKAnimatorComponent sdkAnimatorComponent)
        {
            if (!pbAnimator.IsDirty) return;

            sdkAnimatorComponent.IsDirty = true;
            pbAnimator.IsDirty = false;
            List<SDKAnimationState> sdkAnimationStates = sdkAnimatorComponent.SDKAnimationStates;
            sdkAnimationStates.Clear();

            RepeatedField<PBAnimationState> pbAnimatorStates = pbAnimator.States;

            foreach (PBAnimationState state in pbAnimatorStates)
                sdkAnimationStates.Add(new SDKAnimationState(state));
        }
    }
}
