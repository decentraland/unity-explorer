using Arch.Core;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     Common state for all streamable types
    /// </summary>
    public class StreamableLoadingState
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

        private static readonly ObjectPool<StreamableLoadingState> POOL = new (() => new StreamableLoadingState(),
            actionOnGet: state =>
            {
                state.disposed = false;
                state.Value = Status.NotStarted;
            },
            collectionCheck: PoolConstants.CHECK_COLLECTIONS,
            defaultCapacity: PoolConstants.INITIAL_ASSET_PROMISES_PER_SCENE_COUNT, maxSize: PoolConstants.MAX_ASSET_PROMISES_PER_SCENE_COUNT);

        public static StreamableLoadingState Create() =>
            POOL.Get();

        private bool disposed;

        internal StreamableLoadingState() { }

        public Status Value { get; private set; }

        /// <summary>
        ///     Budget is not null if Status is Allowed or InProgress
        /// </summary>
        public IAcquiredBudget? AcquiredBudget { get; private set; }

        /// <summary>
        ///     Is set when the partial downloading is supported for the given type of asset promise and has started
        /// </summary>
        public PartialLoadingState? PartialDownloadingData { get; internal set; }

        public PartialDownloadStream ClaimOwnershipOverFullyDownloadedData()
        {
            Assert.IsTrue(PartialDownloadingData is { PartialDownloadStream: { IsFullyDownloaded: true } });
            PartialLoadingState value = PartialDownloadingData!.Value;
            PartialDownloadStream owner = value.TransferMemoryOwnership();
            PartialDownloadingData = value;
            return owner;
        }

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
            if (Value is not Status.InProgress && Value is not Status.NotStarted)
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
        public void SetChunkData(PartialLoadingState partialDownloadingData)
        {
            PartialDownloadingData = partialDownloadingData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisposeBudgetIfExists()
        {
            AcquiredBudget?.Dispose();
            AcquiredBudget = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose(Entity entity)
        {
            if (disposed) return;

            disposed = true;

            DisposeBudgetIfExists();

            if (entity == PartialDownloadingData?.PartialDownloadStream.Entity)
                PartialDownloadingData.Value.Dispose();

            PartialDownloadingData = null;
            POOL.Release(this);
        }
    }
}
