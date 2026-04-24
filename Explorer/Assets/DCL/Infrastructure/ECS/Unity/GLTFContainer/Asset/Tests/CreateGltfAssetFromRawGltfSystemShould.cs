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
        public void DisposeGltfDataWhenIntentionIsCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var intention = new GetGltfContainerAssetIntention("raw", "raw_hash", cts);

            // Mirror what LoadSystemBase.ApplyLoadedResult does: cache.AddReference bumps the ref count per consumer.
            var gltfData = new GLTFData(null!, new GameObject("RawGLTF-Root"));
            gltfData.AddReference();

            Entity entity = world.Create(intention, new StreamableLoadingResult<GLTFData>(gltfData));

            system.Update(0);

            // Entity is destroyed to abort the in-flight conversion
            Assert.That(world.IsAlive(entity), Is.False);

            // GLTFData was dereferenced and disposed — otherwise the GameObject and GltfImport outlive the loading entity
            Assert.That(gltfData.CanBeDisposed(), Is.True, "GLTFData.RefCount should have been decremented to zero");
        }

        [Test]
        public void CloneTemplateRootPerConsumer()
        {
            // Simulate the F1 dedup path: two entities receive the same GLTFData reference (like piggy-backers on
            // a single ongoing request). Each must end up with its own Root GameObject so they can be parented to
            // different entity transforms independently — otherwise FinalizeGltfContainerLoadingSystem would reparent
            // the same Root twice and only one entity would render correctly.
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
