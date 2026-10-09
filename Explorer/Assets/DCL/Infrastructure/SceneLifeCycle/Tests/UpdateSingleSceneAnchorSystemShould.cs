using Arch.Core;
using DCL.CharacterMotion.Components;
using ECS.SceneLifeCycle.SingleScene;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.SceneLifeCycle.Tests
{
    public class UpdateSingleSceneAnchorSystemShould : UnitySystemTestBase<UpdateSingleSceneAnchorSystem>
    {
        private SingleSceneMode singleSceneMode = null!;
        private Entity playerEntity;

        [SetUp]
        public void SetUp()
        {
            playerEntity = world.Create();

            singleSceneMode = new SingleSceneMode();
            singleSceneMode.Init(true);

            system = new UpdateSingleSceneAnchorSystem(world, singleSceneMode, playerEntity);
        }

        [Test]
        public void AnchorOnIntentParcelWhenPositionIsNotSet()
        {
            world.Add(playerEntity, new PlayerTeleportIntent(null, new Vector2Int(5, 5), Vector3.zero, CancellationToken.None));

            system.Update(0f);

            Assert.That(singleSceneMode.HasAnchor, Is.True);
            Assert.That(singleSceneMode.AnchorParcel, Is.EqualTo(new Vector2Int(5, 5)));
        }

        [Test]
        public void AnchorOnIntentPositionWhenPositionIsSet()
        {
            // The realm change and movePlayerTo paths leave Parcel at zero and carry the destination in Position
            Vector3 position = ParcelMathHelper.GetPositionByParcelPosition(new Vector2Int(7, 3));

            world.Add(playerEntity, new PlayerTeleportIntent(null, Vector2Int.zero, position, CancellationToken.None, isPositionSet: true));

            system.Update(0f);

            Assert.That(singleSceneMode.AnchorParcel, Is.EqualTo(new Vector2Int(7, 3)));
        }

        [Test]
        public void DoNothingWhenModeIsInactive()
        {
            singleSceneMode.Init(false);

            world.Add(playerEntity, new PlayerTeleportIntent(null, new Vector2Int(5, 5), Vector3.zero, CancellationToken.None));

            system.Update(0f);

            Assert.That(singleSceneMode.HasAnchor, Is.False);
        }

        [Test]
        public void KeepTheAnchorWhileNoNewIntentArrives()
        {
            world.Add(playerEntity, new PlayerTeleportIntent(null, new Vector2Int(5, 5), Vector3.zero, CancellationToken.None));
            system.Update(0f);

            world.Remove<PlayerTeleportIntent>(playerEntity);
            system.Update(0f);

            Assert.That(singleSceneMode.AnchorParcel, Is.EqualTo(new Vector2Int(5, 5)));
        }
    }
}
