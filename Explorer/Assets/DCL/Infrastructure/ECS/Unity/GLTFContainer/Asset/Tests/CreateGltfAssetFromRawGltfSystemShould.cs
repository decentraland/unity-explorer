using Arch.Core;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Tests
{
    [TestFixture]
    public class CreateGltfAssetFromRawGltfSystemShould : UnitySystemTestBase<CreateGltfAssetFromRawGltfSystem>
    {
        private IPerformanceBudget instantiationBudget;
        private IPerformanceBudget memoryBudget;

        [SetUp]
        public void SetUp()
        {
            instantiationBudget = Substitute.For<IPerformanceBudget>();
            instantiationBudget.TrySpendBudget().Returns(true);

            memoryBudget = Substitute.For<IPerformanceBudget>();
            memoryBudget.TrySpendBudget().Returns(true);

            system = new CreateGltfAssetFromRawGltfSystem(world, instantiationBudget, memoryBudget);
        }

        [Test]
        public void LeaveGltfDataDisposalToCacheWhenIntentionIsCancelled()
        {
            // Mirror the real flow: PutAsync inserted the asset into GltfLoadCache, then
            // ApplyLoadedResult called cache.AddReference (refCount = 1). The cancelled consumer
            // must drop its reference but NOT dispose — otherwise the next cache.Unload would
            // double-dispose the same entry (GltfImport disposed twice, Root SafeDestroy'd on an
            // already-destroyed object, totalCount counter decremented twice).
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var intention = new GetGltfContainerAssetIntention("raw", "raw_hash", cts);

            var rawIntention = GetGLTFIntention.Create("raw", "raw_hash");
            var rootGo = new GameObject("RawGLTF-Root");
            var gltfData = new GLTFData(null!, rootGo);

            var cache = new GltfLoadCache();
            cache.Add(rawIntention, gltfData);
            gltfData.AddReference();

            Entity entity = world.Create(intention, new StreamableLoadingResult<GLTFData>(gltfData));

            system.Update(0);

            Assert.That(world.IsAlive(entity), Is.False, "Loading entity must be destroyed");
            Assert.That(gltfData.CanBeDisposed(), Is.True, "Cancelled consumer must drop its reference");
            Assert.That(rootGo != null, Is.True, "Root must remain alive — cache still owns the entry");

            // Cache drain (e.g. the eager drain on LSD reload) finishes disposal exactly once.
            var unlimitedBudget = Substitute.For<IPerformanceBudget>();
            unlimitedBudget.TrySpendBudget().Returns(true);
            cache.Unload(unlimitedBudget, int.MaxValue);

            Assert.That(rootGo == null, Is.True, "Cache.Unload must dispose the GLTFData and destroy Root");
        }

        [Test]
        public void CloneTemplateRootPerConsumer()
        {
            // Two entities receive the same GLTFData reference (piggy-backers on a single ongoing request).
            // Each must end up with its own Root GameObject so they can be parented to different entity
            // transforms independently — otherwise FinalizeGltfContainerLoadingSystem would reparent the
            // same Root twice and only one entity would render correctly.
            var template = new GameObject("RawGLTF-Template");
            var gltfData = new GLTFData(null!, template);
            // Two consumers → two ApplyLoadedResult AddReference calls
            gltfData.AddReference();
            gltfData.AddReference();

            Entity a = world.Create(
                new GetGltfContainerAssetIntention("raw", "raw_hash", new CancellationTokenSource()),
                new StreamableLoadingResult<GLTFData>(gltfData));

            Entity b = world.Create(
                new GetGltfContainerAssetIntention("raw", "raw_hash", new CancellationTokenSource()),
                new StreamableLoadingResult<GLTFData>(gltfData));

            system.Update(0);

            Assert.That(world.TryGet(a, out StreamableLoadingResult<GltfContainerAsset> resultA), Is.True);
            Assert.That(world.TryGet(b, out StreamableLoadingResult<GltfContainerAsset> resultB), Is.True);
            Assert.That(resultA.Succeeded, Is.True);
            Assert.That(resultB.Succeeded, Is.True);

            Assert.That(resultA.Asset!.Root, Is.Not.SameAs(template), "Consumer A should wrap a clone, not the template");
            Assert.That(resultB.Asset!.Root, Is.Not.SameAs(template), "Consumer B should wrap a clone, not the template");
            Assert.That(resultA.Asset.Root, Is.Not.SameAs(resultB.Asset.Root), "Each consumer must own a distinct Root GameObject");

            // Template is still alive (GLTFData.Root reference held inside the cache). Clones are SetActive(false)
            // waiting for FinalizeGltfContainerLoadingSystem to reparent and activate.
            Assert.That(template != null, Is.True, "Template Root must remain alive while consumers hold references");
            Assert.That(resultA.Asset.Root.activeSelf, Is.False);
            Assert.That(resultB.Asset.Root.activeSelf, Is.False);
        }
    }
}
