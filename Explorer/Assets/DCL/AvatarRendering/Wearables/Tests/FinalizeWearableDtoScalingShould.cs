using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Tests;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.SceneLifeCycle.Tests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace DCL.AvatarRendering.Wearables.Tests
{
    /// <summary>
    ///     Pins the O(n^2) -&gt; O(n) rewrite of FinalizeWearableDTO bookkeeping. The old path seeds a
    ///     List&lt;URN&gt; with every pointer and calls List.Remove per resolved DTO (a scan+shift, O(n) each,
    ///     O(n^2) overall); the fix accumulates resolved ids into a HashSet and error-finalizes the
    ///     complement (O(1) per DTO, O(n) overall). Correctness is identical either way, so the falsifiable
    ///     signal for the algorithmic change is scaling: a 4x larger batch must cost ~4x, not ~16x. A bound
    ///     of 8x cleanly separates linear from quadratic. A second test pins the SET of error-finalized
    ///     pointers so a wrong-set rewrite is caught independently of timing.
    /// </summary>
    public class FinalizeWearableDtoScalingShould : UnitySystemTestBase<FinalizeAssetBundleWearableLoadingSystem>
    {
        private WearableStorage storage;

        [SetUp]
        public void SetUp()
        {
            storage = new WearableStorage();
            system = new FinalizeAssetBundleWearableLoadingSystem(world, storage, new RealmData(new TestIpfsRealm()));
            system.Initialize();
        }

        private static WearableDTO MakeDTO(URN urn) =>
            new ()
            {
                id = urn,
                metadata = new WearableDTO.WearableMetadataDto { id = urn },
            };

        private FakeWearable SeedWearable(URN urn)
        {
            if (storage.TryGetElement(urn, out IWearable existing))
                return (FakeWearable)existing;

            WearableDTO dto = MakeDTO(urn);

            // Seed every catalog entry with a SUCCESS model. A resolved pointer keeps it (TryResolveDTO is a
            // no-op on FakeWearable); an unresolved pointer gets it overwritten to a failure by
            // ReportAndFinalizeWithError -> ResolvedFailedDTO. So a failed Model after Update == "was reported".
            var fake = new FakeWearable(dto, model: new StreamableLoadingResult<WearableDTO>(dto));
            storage.wearablesCache.Add(urn, fake);
            return fake;
        }

        /// <summary>
        ///     Builds a promise whose attachment list contains a DTO for every pointer in <paramref name="resolved" />
        ///     (those resolve) but none for <paramref name="unresolved" /> (those must be error-finalized). Every
        ///     pointer, resolved or not, exists in the catalog. Duplicate URNs across the arrays are allowed and
        ///     land in the intention's Pointers list verbatim to exercise the duplicate-pointer edge.
        /// </summary>
        private void BuildPromise(IReadOnlyList<URN> resolved, IReadOnlyList<URN> unresolved)
        {
            var pointers = new List<URN>(resolved.Count + unresolved.Count);
            RepoolableList<WearableDTO> repoolable = RepoolableList<WearableDTO>.NewList();
            List<WearableDTO> dtos = repoolable.List;

            foreach (URN urn in resolved)
            {
                pointers.Add(urn);
                SeedWearable(urn);

                // The attachment DTO only needs the right id for catalog lookup + TryResolveDTO.
                dtos.Add(MakeDTO(urn));
            }

            foreach (URN urn in unresolved)
            {
                pointers.Add(urn);
                SeedWearable(urn);
            }

            var intention = new GetWearableDTOByPointersIntention(pointers, new CommonLoadingArguments(URLAddress.FromString("test")));
            AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> promise =
                AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(world, intention, PartitionComponent.TOP_PRIORITY);

            world.Add(promise.Entity, new StreamableLoadingResult<WearablesDTOList>(new WearablesDTOList(repoolable)));
        }

        private void BuildResolvedPromise(int n)
        {
            var urns = new List<URN>(n);
            for (var i = 0; i < n; i++)
                urns.Add(new URN($"urn:decentraland:off-chain:base-avatars:item_{i}"));

            BuildPromise(urns, System.Array.Empty<URN>());
        }

        private double MedianUpdateMs(int n, int warmup, int measured)
        {
            for (var i = 0; i < warmup; i++)
            {
                BuildResolvedPromise(n);
                system.Update(0);
            }

            var samples = new List<double>(measured);
            var sw = new Stopwatch();

            for (var i = 0; i < measured; i++)
            {
                BuildResolvedPromise(n);
                sw.Restart();
                system.Update(0);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }

        [Test]
        public void ScalesLinearlyWithBatchSize()
        {
            LogAssert.ignoreFailingMessages = true;

            double small = MedianUpdateMs(500, warmup: 3, measured: 10);
            double large = MedianUpdateMs(2000, warmup: 3, measured: 10);

            Debug.Log($"[FinalizeWearableDTO] N=500 median={small}ms N=2000 median={large}ms ratio={large / small:F2}");

            // O(n^2) dev path: 4x pointers -> ~16x work (RED). O(n) fix: ~4x work (GREEN). 8x separates them.
            Assert.That(large, Is.LessThan(8 * small),
                $"Expected ~linear scaling; got N=500={small}ms N=2000={large}ms (ratio {large / small:F2})");
        }

        [Test]
        public void ReportsExactlyTheUnresolvedPointers()
        {
            LogAssert.ignoreFailingMessages = true;

            var resolved = new List<URN>();
            var unresolved = new List<URN>();

            for (var i = 0; i < 6; i++) resolved.Add(new URN($"urn:decentraland:off-chain:base-avatars:res_{i}"));
            for (var i = 0; i < 4; i++) unresolved.Add(new URN($"urn:decentraland:off-chain:base-avatars:unres_{i}"));

            // Duplicate-pointer edge: a resolved and an unresolved pointer each appear twice in the intention.
            // The observable outcome is a SET (a wearable's Model is either success or failure); duplicate
            // unresolved pointers are error-finalized idempotently, so the reported SET is unchanged.
            var pointersResolved = new List<URN>(resolved) { resolved[0] };
            var pointersUnresolved = new List<URN>(unresolved) { unresolved[0] };

            BuildPromise(pointersResolved, pointersUnresolved);
            system.Update(0);

            var observedUnresolved = new HashSet<URN>();
            foreach (URN urn in resolved)
            {
                storage.TryGetElement(urn, out IWearable w);
                if (!w.Model.Succeeded) observedUnresolved.Add(urn);
            }

            foreach (URN urn in unresolved)
            {
                storage.TryGetElement(urn, out IWearable w);
                if (!w.Model.Succeeded) observedUnresolved.Add(urn);
            }

            // Correctness pin, red on any wrong-set rewrite (reports resolved ids, drops unresolved ids, or
            // reports the whole batch). Identical to the old seeded-List.Remove path by construction.
            var expectedUnresolved = new HashSet<URN>(unresolved);
            Assert.That(observedUnresolved, Is.EquivalentTo(expectedUnresolved),
                "The error-finalized pointer set must be exactly the pointers with no resolving DTO.");
        }
    }
}
