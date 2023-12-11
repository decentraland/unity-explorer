using Arch.Core;
using DCL.Billboard.System;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Billboard.Tests
{
    public class BillboardTest
    {
        [Test]
        public void NoRotation()
        {
            var world = World.Create();
            var system = new BillboardSystem(world, new IExposedCameraData.Random());
            var transform = new GameObject().transform;

            world.Create(
                new PBBillboard { BillboardMode = BillboardMode.BmNone },
                new TransformComponent(transform)
            );

            var expected = transform.rotation;

            system.Update(0);

            Assert.AreEqual(expected, transform.rotation);
        }
    }
}
