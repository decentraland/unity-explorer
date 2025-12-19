using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Unity.Profiling;
using Unity.PerformanceTesting;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.CleanUp;
using ECS.StreamableLoading.Cache.Disk.Lock;

namespace DCL.Tests.PlayMode.PerformanceTests
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

        [Test]
        [Performance]
        public void CleanUpIfNeeded()
        {
            ProfilerRecorder gcAlloc =
                ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            Measure
                .Method(cache.CleanUpIfNeeded)
                .WarmupCount(10)
                .MeasurementCount(10)
                .GC()
                .Run();

            Debug.Log($"GC Alloc: {gcAlloc.LastValue} bytes");
        }
    }
}
