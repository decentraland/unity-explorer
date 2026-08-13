using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.ResourcesUnloading;
using ECS.SceneLifeCycle;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.SceneLifeCycle.Tests
{
    public class ECSReloadSceneResetPointAtShould
    {
        private static readonly Vector3 STALE_TARGET = new (1234f, 56f, 7890f);

                private World world = null!;
        private Entity playerEntity;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            playerEntity = world.Create(new HandPointAtComponent { IsPointing = true, WorldHitPoint = STALE_TARGET });
        }

        [TearDown]
        public void TearDown()
        {
            World.Destroy(world);
        }

        [Test]
        public void StopPointingOnSceneReload()
        {
            var reloadScene = new ECSReloadScene(Substitute.For<IScenesCache>(), world, playerEntity, false, Substitute.For<ICacheCleaner>());

            reloadScene.TryReloadSceneAsync(CancellationToken.None, "missing-scene").Forget();

            Assert.That(world.Get<HandPointAtComponent>(playerEntity).IsPointing, Is.False);
        }
    }
}
