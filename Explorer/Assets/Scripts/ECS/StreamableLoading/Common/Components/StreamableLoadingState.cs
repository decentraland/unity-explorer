using DCL.Optimization.PerformanceBudgeting;
using JetBrains.Annotations;
using UnityEngine.Assertions;

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

        public Status Value;

        /// <summary>
        ///     Budget is not null if Status is Allowed or InProgress
        /// </summary>
        public IAcquiredBudget? AcquiredBudget { get; private set; }

        public void SetAllowed(IAcquiredBudget budget)
        {
            AcquiredBudget = budget;
            Value = Status.Allowed;
        }

        public void DisposeBudget()
        {
            Assert.IsNotNull(AcquiredBudget);
            AcquiredBudget?.Dispose();
            AcquiredBudget = null;
        }
    }
}
