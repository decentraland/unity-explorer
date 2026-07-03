using DCL.Ipfs;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Tests
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
            Assert.AreEqual(baseParcel.y * PARCEL + 8f, worldPos.z, EPSILON);
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

        private static SceneEntityDefinition BuildSceneDef(Vector2Int baseParcel, IReadOnlyList<Vector2Int> parcels, SceneMetadata.SpawnPoint spawnPoint)
        {
            var sceneSection = new SceneMetadataScene
            {
                DecodedBase = baseParcel,
                DecodedParcels = parcels,
            };

            var metadata = new SceneMetadata
            {
                scene = sceneSection,
                spawnPoints = new List<SceneMetadata.SpawnPoint> { spawnPoint },
            };

            return new SceneEntityDefinition("test-scene", metadata);
        }

        private static SceneMetadata.SpawnPoint MakeSpawnPoint(
            float[]? xRange = null, float[]? yRange = null, float[]? zRange = null,
            float? xSingle = null, float? ySingle = null, float? zSingle = null,
            Vector3? cameraTarget = null, bool isDefault = false)
        {
            var sp = new SceneMetadata.SpawnPoint
            {
                name = "TestSpawn",
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
