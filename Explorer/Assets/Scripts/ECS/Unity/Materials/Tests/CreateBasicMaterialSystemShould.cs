﻿using Arch.Core;
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
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Materials.Tests
{
    public class CreateBasicMaterialSystemShould : UnitySystemTestBase<CreateBasicMaterialSystem>
    {
        private Material basicMat;

        [SetUp]
        public void SetUp()
        {
            basicMat = Resources.Load<Material>(CreateBasicMaterialSystem.MATERIAL_PATH);
            IObjectPool<Material> pool = Substitute.For<IObjectPool<Material>>();
            pool.Get().Returns(_ => new Material(basicMat));

            system = new CreateBasicMaterialSystem(world, pool);
            system.Initialize();
        }

        [Test]
        public void ConstructMaterial()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            CreateAndFinalizeTexturePromise(ref component.AlbedoTexPromise);

            Entity e = world.Create(component);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(MaterialComponent.LifeCycle.LoadingFinished));

            Assert.That(afterUpdate.Result, Is.Not.Null);
            Assert.That(afterUpdate.Result.shader, Is.EqualTo(basicMat.shader));
        }

        [Test]
        public void NotConstructMaterialIfTexturesLoadingNotFinished()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            component.AlbedoTexPromise = AssetPromise<Texture2D, GetTextureIntention>.Create(world, new GetTextureIntention(), PartitionComponent.TOP_PRIORITY);

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
            new (MaterialData.CreateBasicMaterial(
                new TextureComponent("albedo", TextureWrapMode.Mirror, FilterMode.Point),
                0,
                Color.red,
                false));
    }
}
