using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using DCL.WebRequests.PartialDownload;
using ECS.StreamableLoading.Cache.Disk;
using System;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        private MutexSlim<PartialFile>? memoryOwner;

        public PartialLoadingState(MutexSlim<PartialFile> partialFile)
        {
            memoryOwner = partialFile;
        }

        internal readonly MutexSlim<PartialFile> PeekOwner() =>
            memoryOwner.EnsureNotNull();

        public readonly bool IsFileFullyDownloaded => memoryOwner.EnsureNotNull().Access(static p => p.MetaData.IsFullyDownloaded);

        /// <summary>
        ///     When the memory ownership is transferred, the responsibility to dispose of the memory will be on the external caller
        /// </summary>
        internal MutexSlim<PartialFile> TransferMemoryOwnership()
        {
            if (memoryOwner == null)
                throw new InvalidOperationException("Memory owner is null");

            var memoryOwnerToReturn = memoryOwner;
            memoryOwner = null;
            return memoryOwnerToReturn;
        }

        public readonly void Dispose()
        {
            memoryOwner?.Dispose();
        }
    }

    public static class PartialLoadingStateExtensions
    {
        public static PartialDownloadingRange NewPartialDownloadingRange(this PartialFile partialFile)
        {
            var meta = partialFile.MetaData;

            if (meta.MaxFileSize == 0)

                // If the downloading has not started yet, create the first chunk data
                return new PartialDownloadingRange(0, PartialDownloadingRange.CHUNK_SIZE);

            // If the downloading has already started, get the next chunk data
            int nextRange = partialFile.NextRangeStart;
            int limit = Mathf.Min(meta.MaxFileSize - 1, nextRange + PartialDownloadingRange.CHUNK_SIZE);
            return new PartialDownloadingRange(nextRange, limit);
        }
    }
}
