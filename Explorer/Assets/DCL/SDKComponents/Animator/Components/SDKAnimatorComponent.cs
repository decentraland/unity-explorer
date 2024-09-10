using DCL.Diagnostics;
using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.SDKComponents.Animator.Components
{
    public struct SDKAnimatorComponent : IDisposable
    {
        private readonly List<SDKAnimationState> sdkAnimationStates;
        private bool disposed;

        public bool IsDirty { get; private set; }

        /// <summary>
        ///     Tests only
        /// </summary>
        internal readonly IEnumerable<SDKAnimationState> SDKAnimationStates => sdkAnimationStates;

        private SDKAnimatorComponent(List<SDKAnimationState> sdkAnimationStates)
        {
            this.sdkAnimationStates = sdkAnimationStates;
            disposed = false;
            IsDirty = true;
        }

        public static SDKAnimatorComponent NewComponentFromPbAnimator(PBAnimator pbAnimator)
        {
            List<SDKAnimationState> sdkAnimationStates = ListPool<SDKAnimationState>.Get()!;

            for (var i = 0; i < pbAnimator.States.Count; i++)
            {
                PBAnimationState pbAnimationState = pbAnimator.States[i];
                var sdkAnimationState = new SDKAnimationState(pbAnimationState);
                sdkAnimationStates.Add(sdkAnimationState);
            }

            return new SDKAnimatorComponent(sdkAnimationStates);
        }

        public bool TryConsumeAndUnDirt(out IReadOnlyList<SDKAnimationState> states)
        {
            if (IsDirty)
            {
                IsDirty = false;
                states = sdkAnimationStates;
                return true;
            }

            states = ArraySegment<SDKAnimationState>.Empty;
            return false;
        }

        public void RechargeStates(IEnumerable<PBAnimationState> pbAnimationStates)
        {
            IsDirty = true;
            sdkAnimationStates.Clear();

            foreach (var state in pbAnimationStates)
                sdkAnimationStates.Add(new SDKAnimationState(state));
        }

        public void Dispose()
        {
            if (disposed)
            {
                ReportHub.LogError(ReportCategory.ANIMATOR, "SDKAnimatorComponent an attempt of double disposing occurred");
                return;
            }

            disposed = true;
            ListPool<SDKAnimationState>.Release(sdkAnimationStates);
        }
    }
}
