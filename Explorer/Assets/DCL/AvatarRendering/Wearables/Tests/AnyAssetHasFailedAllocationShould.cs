using DCL.AvatarRendering.AvatarShape.Tests;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Diagnostics;
using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;
using System;

namespace DCL.AvatarRendering.Wearables.Tests
{
    /// <summary>
    /// Pins the allocation-free rewrite of <see cref="FinalizeWearableLoadingSystemBase.AnyAssetHasFailed"/>
    /// (was a LINQ .Any over an array, allocating an enumerator per call) plus its exact null =&gt; true semantics.
    /// </summary>
    public class AnyAssetHasFailedAllocationShould
    {
        private static FakeWearable MakeWearable(params bool[] succeeded)
        {
            var results = new StreamableLoadingResult<AttachmentAssetBase>?[succeeded.Length];

            for (var i = 0; i < succeeded.Length; i++)
                results[i] = succeeded[i]
                    ? new StreamableLoadingResult<AttachmentAssetBase>(new AttachmentRegularAsset(null, null, null))
                    : new StreamableLoadingResult<AttachmentAssetBase>(ReportData.UNSPECIFIED, new Exception("failed"));

            var assets = new WearableAssets[BodyShape.COUNT];
            assets[BodyShape.MALE] = new WearableAssets { Results = results };

            return NewWearable(assets);
        }

        private static FakeWearable NewWearable(WearableAssets[] assets) =>
            new (
                new WearableDTO { id = "urn:x", metadata = new WearableDTO.WearableMetadataDto { id = "urn:x" } },
                wearableAssetResults: assets);

        [Test]
        public void DoesNotAllocate()
        {
            FakeWearable wearable = MakeWearable(true, true);

            // Warm the JIT so we measure the steady state, not first-call compilation.
            FinalizeWearableLoadingSystemBase.AnyAssetHasFailed(wearable, BodyShape.MALE);

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < 1000; i++)
                FinalizeWearableLoadingSystemBase.AnyAssetHasFailed(wearable, BodyShape.MALE);

            long delta = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(delta, Is.EqualTo(0), $"AnyAssetHasFailed allocated {delta} bytes over 1000 calls");
        }

        [Test]
        public void ReturnsFalseWhenAllSucceeded() =>
            Assert.That(FinalizeWearableLoadingSystemBase.AnyAssetHasFailed(MakeWearable(true, true), BodyShape.MALE), Is.False);

        [Test]
        public void ReturnsTrueWhenOneFailed() =>
            Assert.That(FinalizeWearableLoadingSystemBase.AnyAssetHasFailed(MakeWearable(true, false), BodyShape.MALE), Is.True);

        [Test]
        public void ReturnsTrueWhenResultsNull()
        {
            var assets = new WearableAssets[BodyShape.COUNT]; // Results left null for MALE
            FakeWearable wearable = NewWearable(assets);
            Assert.That(FinalizeWearableLoadingSystemBase.AnyAssetHasFailed(wearable, BodyShape.MALE), Is.True);
        }
    }
}
