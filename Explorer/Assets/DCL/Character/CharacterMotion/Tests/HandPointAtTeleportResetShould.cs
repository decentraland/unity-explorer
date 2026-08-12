using Arch.Core;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.CharacterMotion.Systems;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using ECS.TestSuite;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Tests
{
    public class HandPointAtTeleportResetShould : UnitySystemTestBase<HandPointAtSystem>
    {
        private static readonly Vector3 STALE_TARGET = new (1234f, 56f, 7890f);

        [SetUp]
        public void SetUp()
        {
            world.Create(new CameraComponent());

            system = new HandPointAtSystem(world);
            system.Initialize();
        }

        [Test]
        public void StopPointingWhileTeleporting()
        {
            Entity player = world.Create(
                new PlayerComponent(),
                new HandPointAtComponent { IsPointing = true, WorldHitPoint = STALE_TARGET },
                new PlayerTeleportIntent(null, Vector2Int.zero, Vector3.zero, CancellationToken.None));

            system!.Update(0.1f);

            Assert.That(world.Get<HandPointAtComponent>(player).IsPointing, Is.False);
        }

        [Test]
        public void KeepPointingWhenNotTeleporting()
        {
            Entity player = world.Create(
                new PlayerComponent(),
                new HandPointAtComponent { IsPointing = true, WorldHitPoint = STALE_TARGET });

            system!.Update(0.1f);

            Assert.That(world.Get<HandPointAtComponent>(player).IsPointing, Is.True);
        }
    }
}
