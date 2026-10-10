using Arch.Core;
using DCL.Character.CharacterMotion.Systems;
using DCL.CharacterMotion.Components;
using DCL.Ipfs;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Character.CharacterMotion.Tests
{
    public class TeleportPositionCalculationSystemShould : UnitySystemTestBase<TeleportPositionCalculationSystem>
    {
        private const int PARCEL = ParcelMathHelper.PARCEL_SIZE;
        private const float EPSILON = 0.001f;

        [SetUp]
        public void Setup()
        {
            system = new TeleportPositionCalculationSystem(world, Substitute.For<ILandscape>());
        }

        /// <summary>
        ///     A land-on-parcel intent aims at the requested parcel's centre rather than the scene's spawn
        ///     point, so an event at a parcel that holds no spawn point of its own (e.g. the Theatre at 0,5
        ///     inside Genesis Plaza) lands on that parcel. Whether such an intent is raised at all is decided
        ///     upstream by <see cref="TeleportUtils.TryPickSpawnPointNameInParcel" />.
        /// </summary>
        [Test]
        public void AimAtParcelCentreWhenIntentLandsOnParcel()
        {
            var baseParcel = new Vector2Int(0, 0);
            var eventParcel = new Vector2Int(0, 5);

            var parcels = new List<Vector2Int>();

            for (int y = baseParcel.y; y <= eventParcel.y; y++)
                parcels.Add(new Vector2Int(0, y));

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                parcels,
                MakeSpawnPoint(x: 2f, z: 2f));

            Entity entity = world.Create(new PlayerTeleportIntent(sceneDef, eventParcel, Vector3.zero, CancellationToken.None, landOnParcel: true));

            system!.Update(0);

            Vector3 position = world.Get<PlayerTeleportIntent>(entity).Position;

            Assert.That(position.x, Is.EqualTo((eventParcel.x * PARCEL) + ParcelMathHelper.HALF_PARCEL_SIZE).Within(EPSILON));
            Assert.That(position.z, Is.EqualTo((eventParcel.y * PARCEL) + ParcelMathHelper.HALF_PARCEL_SIZE).Within(EPSILON));
        }

        private static SceneEntityDefinition BuildSceneDef(Vector2Int baseParcel, IReadOnlyList<Vector2Int> parcels, params SceneMetadata.SpawnPoint[] spawnPoints)
        {
            var sceneSection = new SceneMetadataScene
            {
                DecodedBase = baseParcel,
                DecodedParcels = parcels,
            };

            var metadata = new SceneMetadata
            {
                scene = sceneSection,
                spawnPoints = new List<SceneMetadata.SpawnPoint>(spawnPoints),
            };

            return new SceneEntityDefinition("test-scene", metadata);
        }

        private static SceneMetadata.SpawnPoint MakeSpawnPoint(float x, float z) =>
            new ()
            {
                name = "TestSpawn",
                @default = true,
                position = new SceneMetadata.SpawnPoint.Position
                {
                    x = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = x },
                    y = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = 0f },
                    z = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = z },
                },
            };
    }
}
