using Arch.Core;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.CharacterMotion.Systems;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Tests
{
    public class HandPointAtStaleResetShould : UnitySystemTestBase<HandPointAtSystem>
    {
        private static readonly Vector3 STALE_TARGET = new (1234f, 56f, 7890f);

        private ReactiveProperty<ISceneFacade?> currentScene = null!;

        [SetUp]
        public void SetUp()
        {
            world.Create(new CameraComponent());

            currentScene = new ReactiveProperty<ISceneFacade?>(Substitute.For<ISceneFacade>());

            var scenesCache = Substitute.For<IScenesCache>();
            scenesCache.CurrentScene.Returns(currentScene);

            system = new HandPointAtSystem(world, scenesCache);
            system.Initialize();
        }

        [Test]
        public void StopPointingWhileTeleporting()
        {
            Entity player = CreatePointingPlayer(
                new PlayerTeleportIntent(null, Vector2Int.zero, Vector3.zero, CancellationToken.None));

            system!.Update(0.1f);

            Assert.That(world.Get<HandPointAtComponent>(player).IsPointing, Is.False);
        }

        [Test]
        public void StopPointingWhenCurrentSceneIsLost()
        {
            Entity player = CreatePointingPlayer();

            currentScene.Value = null;
            system!.Update(0.1f);

            Assert.That(world.Get<HandPointAtComponent>(player).IsPointing, Is.False);
        }

        [Test]
        public void KeepPointingWhenCurrentSceneChanges()
        {
            Entity player = CreatePointingPlayer();

            currentScene.Value = Substitute.For<ISceneFacade>();
            system!.Update(0.1f);

            Assert.That(world.Get<HandPointAtComponent>(player).IsPointing, Is.True);
        }

        [Test]
        public void KeepPointingWhenNotTeleporting()
        {
            Entity player = CreatePointingPlayer();

            system!.Update(0.1f);

            Assert.That(world.Get<HandPointAtComponent>(player).IsPointing, Is.True);
        }

        private Entity CreatePointingPlayer() =>
            world.Create(
                new PlayerComponent(),
                new HandPointAtComponent { IsPointing = true, WorldHitPoint = STALE_TARGET });

        private Entity CreatePointingPlayer(PlayerTeleportIntent teleportIntent) =>
            world.Create(
                new PlayerComponent(),
                new HandPointAtComponent { IsPointing = true, WorldHitPoint = STALE_TARGET },
                teleportIntent);
    }
}
