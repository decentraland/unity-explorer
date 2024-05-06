using DCL.ECSComponents;
using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Animator.Components
{
    public struct SDKAnimatorComponent : IDisposable
    {
        public bool IsDirty;
        private readonly List<SDKAnimationState> list;

        public readonly IReadOnlyList<SDKAnimationState> SDKAnimationStates => list;

        public SDKAnimatorComponent(PBAnimator pbAnimator)
        {
            list = ListPool<SDKAnimationState>.Get()!;

            for (var i = 0; i < pbAnimator.States!.Count; i++)
            {
                PBAnimationState pbAnimationState = pbAnimator.States[i]!;
                var sdkAnimationState = new SDKAnimationState(pbAnimationState);
                list.Add(sdkAnimationState);
            }

            IsDirty = true;
        }

        public void ClearStateAndApply(RepeatedField<PBAnimationState> pbAnimatorStates)
        {
            list.Clear();

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < pbAnimatorStates.Count; i++)
            {
                var sdkAnimationState = new SDKAnimationState(pbAnimatorStates[i]!);
                list.Add(sdkAnimationState);
            }
        }

        public void Dispose()
        {
            ListPool<SDKAnimationState>.Release(list);
        }
    }
}
