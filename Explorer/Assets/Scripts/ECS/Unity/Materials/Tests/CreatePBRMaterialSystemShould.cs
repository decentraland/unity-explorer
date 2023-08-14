using Arch.Core;
using Cysharp.Threading.Tasks;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ECS.Unity.Materials.Tests
{
    public class CreatePBRMaterialSystemShould : UnitySystemTestBase<CreatePBRMaterialSystem>
    {
        private Material pbrMat;

        [SetUp]
        public async Task SetUp()
        {
            AsyncOperationHandle<Material> loadMaterialTask = Addressables.LoadAssetAsync<Material>("ShapeMaterial");
            await loadMaterialTask.Task;
            pbrMat = loadMaterialTask.Result;

            IObjectPool<Material> pool = Substitute.For<IObjectPool<Material>>();
            pool.Get().Returns(_ => new Material(pbrMat));

            IConcurrentBudgetProvider frameTimeBudgetProvider = Substitute.For<IConcurrentBudgetProvider>();
            frameTimeBudgetProvider.TrySpendBudget().Returns(true);

            system = new CreatePBRMaterialSystem(world, pool, frameTimeBudgetProvider);
            system.Initialize();
        }

        [Test]
        public async Task ConstructMaterial()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await UniTask.WaitUntil(() => system != null);

            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            CreateAndFinalizeTexturePromise(ref component.AlbedoTexPromise);
            CreateAndFinalizeTexturePromise(ref component.AlphaTexPromise);
            CreateAndFinalizeTexturePromise(ref component.EmissiveTexPromise);
            CreateAndFinalizeTexturePromise(ref component.BumpTexPromise);

            Entity e = world.Create(component);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(MaterialComponent.LifeCycle.LoadingFinished));

            Assert.That(afterUpdate.Result, Is.Not.Null);
            Assert.That(afterUpdate.Result.shader, Is.EqualTo(pbrMat.shader));
        }

        [Test]
        public async Task NotConstructMaterialIfTexturesLoadingNotFinished()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await UniTask.WaitUntil(() => system != null);

            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            CreateAndFinalizeTexturePromise(ref component.AlbedoTexPromise);
            CreateAndFinalizeTexturePromise(ref component.AlphaTexPromise);

            component.BumpTexPromise = AssetPromise<Texture2D, GetTextureIntention>.Create(world, new GetTextureIntention(), PartitionComponent.TOP_PRIORITY);
            component.EmissiveTexPromise = AssetPromise<Texture2D, GetTextureIntention>.Create(world, new GetTextureIntention(), PartitionComponent.TOP_PRIORITY);

            Entity e = world.Create(component);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(MaterialComponent.LifeCycle.LoadingInProgress));

            Assert.That(afterUpdate.Result, Is.Null);
        }

        private void CreateAndFinalizeTexturePromise(ref AssetPromise<Texture2D, GetTextureIntention>? promise)
        {
            promise = AssetPromise<Texture2D, GetTextureIntention>.Create(world, new GetTextureIntention(), PartitionComponent.TOP_PRIORITY);
            world.Add(promise.Value.Entity, new StreamableLoadingResult<Texture2D>(Texture2D.grayTexture));
        }

        internal static MaterialComponent CreateMaterialComponent() =>
            new (MaterialData.CreatePBRMaterial(
                new TextureComponent("albedo", TextureWrapMode.Mirror, FilterMode.Point),
                new TextureComponent("alpha", TextureWrapMode.Mirror, FilterMode.Trilinear),
                new TextureComponent("emissive", TextureWrapMode.Mirror, FilterMode.Bilinear),
                new TextureComponent("bump", TextureWrapMode.Mirror, FilterMode.Point),
                0.5f,
                true,
                Color.red,
                Color.green,
                Color.blue,
                MaterialTransparencyMode.AlphaBlend,
                0.3f,
                0.4f,
                0.5f,
                0.6f,
                0.7f,
                0
            ));
    }
}
