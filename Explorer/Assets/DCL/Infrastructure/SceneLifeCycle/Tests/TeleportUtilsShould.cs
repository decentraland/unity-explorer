using DCL.Ipfs;
using ECS.SceneLifeCycle;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.SceneLifeCycle.Tests
{
    public class TeleportUtilsShould
    {
        private const int PARCEL = ParcelMathHelper.PARCEL_SIZE;
        private const float EPSILON = 0.001f;

        [Test]
        public void ClampOutOfBoundsRangeToParcelOnSingleParcelScene()
        {
            var baseParcel = new Vector2Int(17, 59);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(
                    xRange: new[] { -117f, 71f },
                    yRange: new[] { 0f, 0f },
                    zRange: new[] { -23f, 165f },
                    cameraTarget: new Vector3(8f, 1f, 8f),
                    isDefault: true));

            float minWorldX = baseParcel.x * PARCEL;
            float maxWorldX = (baseParcel.x + 1) * PARCEL;
            float minWorldZ = baseParcel.y * PARCEL;
            float maxWorldZ = (baseParcel.y + 1) * PARCEL;

            for (var i = 0; i < 200; i++)
            {
                (Vector3 worldPos, Vector3? cameraTarget) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel);

                Assert.GreaterOrEqual(worldPos.x, minWorldX);
                Assert.LessOrEqual(worldPos.x, maxWorldX + EPSILON);
                Assert.GreaterOrEqual(worldPos.z, minWorldZ);
                Assert.LessOrEqual(worldPos.z, maxWorldZ + EPSILON);
                Assert.GreaterOrEqual(worldPos.y, 0f);

                Assert.NotNull(cameraTarget);
                Assert.AreEqual(minWorldX + 8f, cameraTarget!.Value.x, EPSILON);
                Assert.AreEqual(1f, cameraTarget.Value.y, EPSILON);
                Assert.AreEqual(minWorldZ + 8f, cameraTarget.Value.z, EPSILON);
            }
        }

        [Test]
        public void ClampOutOfBoundsSingleValueToParcelEdge()
        {
            var baseParcel = new Vector2Int(17, 59);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(
                    xSingle: -50f,
                    ySingle: 0f,
                    zSingle: 8f,
                    isDefault: true));

            (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel);

            Assert.AreEqual(baseParcel.x * PARCEL, worldPos.x, EPSILON);
            Assert.AreEqual((baseParcel.y * PARCEL) + 8f, worldPos.z, EPSILON);
        }

        [Test]
        public void NotClampWhenRangeIsInsideMultiParcelScene()
        {
            var baseParcel = new Vector2Int(0, 0);
            var parcels = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                parcels,
                MakeSpawnPoint(
                    xRange: new[] { 4f, 20f },
                    ySingle: 0f,
                    zSingle: 8f,
                    isDefault: true));

            for (var i = 0; i < 100; i++)
            {
                (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel);

                Assert.GreaterOrEqual(worldPos.x, 4f);
                Assert.LessOrEqual(worldPos.x, 20f + EPSILON);
                Assert.AreEqual(8f, worldPos.z, EPSILON);
            }
        }

        [Test]
        public void ClampNegativeYToZero()
        {
            var baseParcel = new Vector2Int(0, 0);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(
                    xSingle: 8f,
                    yRange: new[] { -50f, -10f },
                    zSingle: 8f,
                    isDefault: true));

            (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel);

            Assert.AreEqual(0f, worldPos.y, EPSILON);
        }

        [Test]
        public void PickNamedSpawnPointOverDefault()
        {
            var baseParcel = new Vector2Int(0, 0);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "main"),
                MakeSpawnPoint(xSingle: 8f, ySingle: 0f, zSingle: 8f, name: "lobby"));

            (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel, "lobby");

            Assert.AreEqual(8f, worldPos.x, EPSILON);
            Assert.AreEqual(8f, worldPos.z, EPSILON);
        }

        [Test]
        public void MatchSpawnPointNameCaseInsensitively()
        {
            var baseParcel = new Vector2Int(0, 0);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "main"),
                MakeSpawnPoint(xSingle: 8f, ySingle: 0f, zSingle: 8f, name: "Lobby"));

            (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel, "lOBBY");

            Assert.AreEqual(8f, worldPos.x, EPSILON);
            Assert.AreEqual(8f, worldPos.z, EPSILON);
        }

        [Test]
        public void FallBackToDefaultSelectionWhenNameNotMatched()
        {
            var baseParcel = new Vector2Int(0, 0);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "main"),
                MakeSpawnPoint(xSingle: 8f, ySingle: 0f, zSingle: 8f, name: "lobby"));

            (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel, "missing");

            Assert.AreEqual(2f, worldPos.x, EPSILON);
            Assert.AreEqual(2f, worldPos.z, EPSILON);
        }

        [Test]
        public void PickFirstSpawnPointWhenNamesDuplicate()
        {
            var baseParcel = new Vector2Int(0, 0);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "main"),
                MakeSpawnPoint(xSingle: 8f, ySingle: 0f, zSingle: 8f, name: "lobby"),
                MakeSpawnPoint(xSingle: 12f, ySingle: 0f, zSingle: 12f, name: "lobby"));

            (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel, "lobby");

            Assert.AreEqual(8f, worldPos.x, EPSILON);
            Assert.AreEqual(8f, worldPos.z, EPSILON);
        }

        [Test]
        public void ApplyNamedSpawnPointCameraTarget()
        {
            var baseParcel = new Vector2Int(0, 0);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "main"),
                MakeSpawnPoint(xSingle: 8f, ySingle: 0f, zSingle: 8f, name: "lobby", cameraTarget: new Vector3(10f, 1f, 10f)));

            (_, Vector3? cameraTarget) = TeleportUtils.PickTargetWithOffset(sceneDef, baseParcel, "lobby");

            Assert.NotNull(cameraTarget);
            Assert.AreEqual(10f, cameraTarget!.Value.x, EPSILON);
            Assert.AreEqual(1f, cameraTarget.Value.y, EPSILON);
            Assert.AreEqual(10f, cameraTarget.Value.z, EPSILON);
        }

        [Test]
        public void AnchorNamedSpawnPointToSceneBaseNotTargetParcel()
        {
            var baseParcel = new Vector2Int(0, 0);
            var parcels = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                parcels,
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "main"),
                MakeSpawnPoint(xSingle: 20f, ySingle: 0f, zSingle: 8f, name: "lobby"));

            (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, new Vector2Int(1, 0), "lobby");

            Assert.AreEqual(20f, worldPos.x, EPSILON);
            Assert.AreEqual(8f, worldPos.z, EPSILON);
        }

        [Test]
        public void ScatterWithinNamedRangeRegardlessOfTargetParcel()
        {
            var baseParcel = new Vector2Int(0, 0);
            var parcels = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                parcels,
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "main"),
                MakeSpawnPoint(xRange: new[] { 26f, 38f }, ySingle: 0f, zSingle: 8f, name: "scatter"));

            var distinctLandings = new HashSet<float>();

            for (var i = 0; i < 100; i++)
            {
                (Vector3 worldPos, _) = TeleportUtils.PickTargetWithOffset(sceneDef, new Vector2Int(2, 0), "scatter");

                Assert.GreaterOrEqual(worldPos.x, 26f);
                Assert.LessOrEqual(worldPos.x, 38f + EPSILON);
                Assert.AreEqual(8f, worldPos.z, EPSILON);

                distinctLandings.Add(worldPos.x);
            }

            Assert.Greater(distinctLandings.Count, 1, "Players must scatter within the range, not stack on one point");
        }

        /// <summary>
        ///     The "BBQ Sauce Recipe" event at -148,141: the event parcel is the very parcel holding the
        ///     scene's spawn point, so a teleport aimed at it must land on that spawn point instead of the
        ///     parcel centre, where the scene's centrepiece asset stands.
        /// </summary>
        [Test]
        public void PickTheSpawnPointStandingInTheRequestedParcel()
        {
            var baseParcel = new Vector2Int(-148, 141);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { new Vector2Int(-148, 142), new Vector2Int(-147, 142), baseParcel, new Vector2Int(-147, 141) },
                MakeSpawnPoint(
                    xRange: new[] { 0f, 3f },
                    yRange: new[] { 0f, 0f },
                    zRange: new[] { 0f, 3f },
                    cameraTarget: new Vector3(8f, 1f, 8f),
                    isDefault: true,
                    name: "SpawnArea1"));

            Assert.That(TeleportUtils.TryPickSpawnPointNameInParcel(sceneDef, baseParcel, out string spawnPointName), Is.True);
            Assert.That(spawnPointName, Is.EqualTo("SpawnArea1"));
        }

        /// <summary>
        ///     The original land-on-parcel motivation: an event at a parcel that holds no spawn point of its
        ///     own (e.g. the Theatre at 0,5 inside Genesis Plaza) must keep landing on that parcel.
        /// </summary>
        [Test]
        public void PickNoSpawnPointForParcelThatHoldsNone()
        {
            var baseParcel = new Vector2Int(0, 0);
            var farParcel = new Vector2Int(0, 5);

            var parcels = new List<Vector2Int>();

            for (int y = baseParcel.y; y <= farParcel.y; y++)
                parcels.Add(new Vector2Int(0, y));

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                parcels,
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "main"));

            Assert.That(TeleportUtils.TryPickSpawnPointNameInParcel(sceneDef, farParcel, out _), Is.False);
            Assert.That(TeleportUtils.TryPickSpawnPointNameInParcel(sceneDef, baseParcel, out _), Is.True);
        }

        /// <summary>
        ///     A spawn point designated for the requested parcel wins over a default standing elsewhere: the
        ///     request names a parcel, and honouring the default instead would drop the player even further
        ///     from the spot he asked for.
        /// </summary>
        [Test]
        public void PreferTheSpawnPointInTheParcelOverACloserDefault()
        {
            var baseParcel = new Vector2Int(0, 0);
            var farParcel = new Vector2Int(0, 3);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel, new Vector2Int(0, 1), new Vector2Int(0, 2), farParcel },
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: "entrance"),
                MakeSpawnPoint(xSingle: 8f, ySingle: 0f, zSingle: 50f, name: "stage"));

            Assert.That(TeleportUtils.TryPickSpawnPointNameInParcel(sceneDef, farParcel, out string spawnPointName), Is.True);
            Assert.That(spawnPointName, Is.EqualTo("stage"));
        }

        /// <summary>
        ///     A nameless spawn point cannot be addressed through <see cref="TeleportUtils.PickTargetWithOffset" />,
        ///     so it must not suppress the land-on-parcel fallback.
        /// </summary>
        [Test]
        public void PickNoSpawnPointWhenTheOneInTheParcelIsNameless()
        {
            var baseParcel = new Vector2Int(0, 0);

            SceneEntityDefinition sceneDef = BuildSceneDef(
                baseParcel,
                new[] { baseParcel },
                MakeSpawnPoint(xSingle: 2f, ySingle: 0f, zSingle: 2f, isDefault: true, name: ""));

            Assert.That(TeleportUtils.TryPickSpawnPointNameInParcel(sceneDef, baseParcel, out _), Is.False);
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

        private static SceneMetadata.SpawnPoint MakeSpawnPoint(
            float[]? xRange = null, float[]? yRange = null, float[]? zRange = null,
            float? xSingle = null, float? ySingle = null, float? zSingle = null,
            Vector3? cameraTarget = null, bool isDefault = false, string name = "TestSpawn")
        {
            var sp = new SceneMetadata.SpawnPoint
            {
                name = name,
                @default = isDefault,
                position = new SceneMetadata.SpawnPoint.Position
                {
                    x = MakeCoordinate(xRange, xSingle),
                    y = MakeCoordinate(yRange, ySingle),
                    z = MakeCoordinate(zRange, zSingle),
                },
            };

            if (cameraTarget.HasValue)
            {
                Vector3 target = cameraTarget.Value;
                sp.cameraTarget = new SceneMetadata.SpawnPoint.Position
                {
                    x = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = target.x },
                    y = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = target.y },
                    z = new SceneMetadata.SpawnPoint.Coordinate { SingleValue = target.z },
                };
            }

            return sp;
        }

        private static SceneMetadata.SpawnPoint.Coordinate MakeCoordinate(float[]? range, float? single)
        {
            if (range != null)
                return new SceneMetadata.SpawnPoint.Coordinate { MultiValue = range };

            if (single.HasValue)
                return new SceneMetadata.SpawnPoint.Coordinate { SingleValue = single.Value };

            return default(SceneMetadata.SpawnPoint.Coordinate);
        }
    }
}
