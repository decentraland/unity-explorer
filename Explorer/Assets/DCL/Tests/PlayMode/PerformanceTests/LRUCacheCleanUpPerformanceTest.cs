using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Profiling;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;
using Unity.PerformanceTesting;

namespace ECS.StreamableLoading.Cache.Tests
{
    public class LRUCacheCleanUpPerformanceTest
    {
        private LRUDiskCleanUp cache;

        [SetUp]
        public void Setup()
        {
            CacheDirectory directory = CacheDirectory.NewDefault();
            FilesLock filesLock = new FilesLock();
            cache = new LRUDiskCleanUp(directory, filesLock);
        }

        [Test, Performance]
        public void CleanUpIfNeeded()
        {
            cache.CleanUpIfNeeded();
        }
    }
}
