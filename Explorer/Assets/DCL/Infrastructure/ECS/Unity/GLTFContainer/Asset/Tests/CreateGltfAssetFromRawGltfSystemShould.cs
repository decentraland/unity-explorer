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

            // Mirror what LoadGLTFSystem produces: a GLTFData with RefCount=1 ready to be wrapped into a GltfContainerAsset
            var gltfData = new GLTFData(null!, new GameObject("RawGLTF-Root"));
            gltfData.AddReference();

            Entity entity = world.Create(intention, new StreamableLoadingResult<GLTFData>(gltfData));

            system.Update(0);

            // Entity is destroyed to abort the in-flight conversion
            Assert.That(world.IsAlive(entity), Is.False);

            // GLTFData was dereferenced and disposed — otherwise the GameObject and GltfImport outlive the loading entity
            Assert.That(gltfData.CanBeDisposed(), Is.True, "GLTFData.RefCount should have been decremented to zero");
        }
    }
}
