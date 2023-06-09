﻿using Arch.Core;
using DCL.ECSComponents;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.PrimitiveRenderer.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Utility.Primitives;

namespace ECS.Unity.Materials.Tests
{
    public class ApplyMaterialSystemShould : UnitySystemTestBase<ApplyMaterialSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new ApplyMaterialSystem(world);
        }

        [Test]
        public void ApplyMaterialIfLoadingFinished()
        {
            MeshRenderer renderer = new GameObject(nameof(ApplyMaterialIfLoadingFinished)).AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;

            Material mat = DefaultMaterial.New();

            var matComp = new MaterialComponent { Status = MaterialComponent.LifeCycle.LoadingFinished, Result = mat };

            Entity e = world.Create(new PBMaterial(), matComp, new PBMeshRenderer(), new PrimitiveMeshRendererComponent { MeshRenderer = renderer });

            system.Update(0);

            Assert.That(renderer.sharedMaterial, Is.EqualTo(mat));
            Assert.That(world.Get<MaterialComponent>(e).Status, Is.EqualTo(MaterialComponent.LifeCycle.MaterialApplied));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void ApplyMaterialIfRendererIsDirty()
        {
            MeshRenderer renderer = new GameObject(nameof(ApplyMaterialIfLoadingFinished)).AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;

            Material mat = DefaultMaterial.New();

            var matComp = new MaterialComponent { Status = MaterialComponent.LifeCycle.MaterialApplied, Result = mat };

            Entity e = world.Create(new PBMaterial(), matComp, new PBMeshRenderer { IsDirty = true }, new PrimitiveMeshRendererComponent { MeshRenderer = renderer });

            system.Update(0);

            Assert.That(renderer.sharedMaterial, Is.EqualTo(mat));
            Assert.That(world.Get<MaterialComponent>(e).Status, Is.EqualTo(MaterialComponent.LifeCycle.MaterialApplied));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }
    }
}
