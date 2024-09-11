using DCL.Optimization.PerformanceBudgeting;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Utility;

namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     Common state for all streamable types
    /// </summary>
    public struct StreamableLoadingState
    {
        public enum Status : byte
        {
            /// <summary>
            ///     The state was not evaluated yet
            /// </summary>
            NotStarted,

            /// <summary>
            ///     The state was evaluated as Allowed but the loading is not started yet
            /// </summary>
            Allowed,

            /// <summary>
            ///     The state was evaluated as Forbidden
            /// </summary>
            Forbidden,

            /// <summary>
            ///     Loading is in progress
            /// </summary>
            InProgress,

            /// <summary>
            ///     StreamableLoadingResult is ready
            /// </summary>
            Finished,
        }

        public Status Value { get; private set; }

        /// <summary>
        ///     Budget is not null if Status is Allowed or InProgress
        /// </summary>
        public IAcquiredBudget? AcquiredBudget { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAllowed(IAcquiredBudget budget)
        {
#if UNITY_EDITOR
            if (Value is not Status.Forbidden && Value is not Status.NotStarted)
                throw new InvalidOperationException($"Unexpected transition from \"{Value}\" to \"Allowed\"");
#endif
            AcquiredBudget = budget;
            Value = Status.Allowed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Forbid()
        {
#if UNITY_EDITOR
            if (Value is Status.Finished)
                throw new InvalidOperationException($"Unexpected transition from \"{Value}\" to \"Forbidden\"");
#endif
            Value = Status.Forbidden;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void StartProgress()
        {
#if UNITY_EDITOR
            if (Value is not Status.Allowed)
                throw new InvalidOperationException($"Unexpected transition from \"{Value}\" to \"InProgress\"");
#endif
            Value = Status.InProgress;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Finish()
        {
#if UNITY_EDITOR
            if (Value is not Status.InProgress)
                throw new InvalidOperationException($"Unexpected transition from \"{Value}\" to \"Finished\"");
#endif
            Value = Status.Finished;
        }

        /// <summary>
        ///     Indicate that it should be reevaluated
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RequestReevaluate()
        {
#if UNITY_EDITOR
            if (Value is not Status.InProgress)
                throw new InvalidOperationException($"Unexpected transition from \"{Value}\" to \"NotStarted\"");
#endif
            Value = Status.NotStarted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisposeBudgetIfExists()
        {
            AcquiredBudget?.Dispose();
            AcquiredBudget = null;
        }
    }
}
