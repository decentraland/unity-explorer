﻿using Arch.Core;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Materials.Tests
{
    public class CreatePBRMaterialSystemShould : UnitySystemTestBase<CreatePBRMaterialSystem>
    {
        private Material pbrMat;

        [SetUp]
        public void SetUp()
        {
            pbrMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/DCL/Infrastructure/ECS/Unity/Materials/MaterialReference/ShapeMaterial.mat");

            IObjectPool<Material> pool = Substitute.For<IObjectPool<Material>>()!;
            pool.Get().Returns(_ => new Material(pbrMat));

            IReleasablePerformanceBudget frameTimeBudget = Substitute.For<IReleasablePerformanceBudget>()!;
            frameTimeBudget.TrySpendBudget().Returns(true);

            system = new CreatePBRMaterialSystem(world!, pool, frameTimeBudget, frameTimeBudget);
            system.Initialize();
        }

        [Test]
        public void ConstructMaterial()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            CreateAndFinalizeTexturePromise(ref component.AlbedoTexPromise);
            CreateAndFinalizeTexturePromise(ref component.AlphaTexPromise);
            CreateAndFinalizeTexturePromise(ref component.EmissiveTexPromise);
            CreateAndFinalizeTexturePromise(ref component.BumpTexPromise);

            Entity e = world!.Create(component, new ShouldInstanceMaterialComponent());

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(StreamableLoading.LifeCycle.LoadingFinished));

            Assert.That(afterUpdate.Result, Is.Not.Null);
            Assert.That(afterUpdate.Result.shader, Is.EqualTo(pbrMat.shader));
        }

        [Test]
        public void NotConstructMaterialIfTexturesLoadingNotFinished()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            CreateAndFinalizeTexturePromise(ref component.AlbedoTexPromise);
            CreateAndFinalizeTexturePromise(ref component.AlphaTexPromise);

            component.BumpTexPromise = AssetPromise<Texture2DData, GetTextureIntention>.Create(world, new GetTextureIntention(), PartitionComponent.TOP_PRIORITY);
            component.EmissiveTexPromise = AssetPromise<Texture2DData, GetTextureIntention>.Create(world, new GetTextureIntention(), PartitionComponent.TOP_PRIORITY);

            Entity e = world.Create(component, new ShouldInstanceMaterialComponent());

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(StreamableLoading.LifeCycle.LoadingInProgress));

            Assert.That(afterUpdate.Result, Is.Null);
        }

        private void CreateAndFinalizeTexturePromise(ref AssetPromise<Texture2DData, GetTextureIntention>? promise)
        {
            promise = AssetPromise<Texture2DData, GetTextureIntention>.Create(world, new GetTextureIntention(), PartitionComponent.TOP_PRIORITY);
            world.Add(promise.Value.Entity, new StreamableLoadingResult<Texture2DData>(new Texture2DData(Texture2D.grayTexture)));
        }

        internal static MaterialComponent CreateMaterialComponent() =>
            new (MaterialData.CreatePBRMaterial(
                new TextureComponent("albedo",string.Empty, TextureWrapMode.Mirror, FilterMode.Point),
                new TextureComponent("alpha",string.Empty, TextureWrapMode.Mirror, FilterMode.Trilinear),
                new TextureComponent("emissive",string.Empty, TextureWrapMode.Mirror, FilterMode.Bilinear),
                new TextureComponent("bump",string.Empty, TextureWrapMode.Mirror, FilterMode.Point, TextureType.NormalMap),
                0.5f,
                true,
                Color.red,
                Color.green,
                Color.blue,
                MaterialTransparencyMode.AlphaBlend,
                0.3f,
                0.4f,
                0.6f,
                0.7f,
                0
            ));
    }
}
