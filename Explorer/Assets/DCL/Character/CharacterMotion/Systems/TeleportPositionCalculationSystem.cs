using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using UnityEngine;
using Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;
using Random = System.Random;

namespace DCL.Character.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class TeleportPositionCalculationSystem : BaseUnityLoopSystem
    {
        private static readonly Random RANDOM = new ();

        private SingleInstanceEntity? cameraCached;
        private SingleInstanceEntity cameraEntity => cameraCached ??= World.CacheCamera();

        public TeleportPositionCalculationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            CalculateTeleportPositionQuery(World);
        }

        [Query]
        private void CalculateTeleportPosition(in Entity playerEntity, ref PlayerTeleportIntent teleportIntent)
        {
            if (teleportIntent.IsPositionSet) return;

            if (teleportIntent.SceneDef == null)
            {
                teleportIntent.Position = ParcelMathHelper.GetPositionByParcelPosition(teleportIntent.Parcel).WithErrorCompensation().WithTerrainOffset();
            }
            else if (TeleportUtils.IsTramLine(teleportIntent.SceneDef.metadata.OriginalJson.AsSpan()))
            {
                teleportIntent.Position = ParcelMathHelper.GetPositionByParcelPosition(teleportIntent.Parcel).WithErrorCompensation();
            }
            else
            {
                (Vector3 targetWorldPosition, Vector3? cameraTarget) = PickTargetWithOffset(teleportIntent.SceneDef, teleportIntent.Parcel);

                teleportIntent.Position = targetWorldPosition;

                if (cameraTarget != null)
                {
                    World?.AddOrGet(cameraEntity, new CameraLookAtIntent(cameraTarget.Value, targetWorldPosition));
                    World?.AddOrGet(playerEntity, new PlayerLookAtIntent(cameraTarget.Value, targetWorldPosition));
                }
            }

            teleportIntent.IsPositionSet = true;
        }

        private static (Vector3 targetWorldPosition, Vector3? cameraTarget) PickTargetWithOffset(SceneEntityDefinition? sceneDef, Vector2Int parcel)
        {
            Vector3? cameraTarget = null;

            Vector3 parcelBaseWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithErrorCompensation();
            Vector3 targetWorldPosition = parcelBaseWorldPosition;

            List<SceneMetadata.SpawnPoint>? spawnPoints = sceneDef.metadata.spawnPoints;

            if (spawnPoints is { Count: > 0 })
            {
                SceneMetadata.SpawnPoint spawnPoint = PickSpawnPoint(spawnPoints, targetWorldPosition, parcelBaseWorldPosition);

                // TODO validate offset position is within bounds of one of scene parcels
                targetWorldPosition += GetSpawnPositionOffset(spawnPoint);

                if (spawnPoint.cameraTarget != null)
                    cameraTarget = spawnPoint.cameraTarget!.Value.ToVector3() + parcelBaseWorldPosition;
            }

            return (targetWorldPosition, cameraTarget);
        }

        private static SceneMetadata.SpawnPoint PickSpawnPoint(IReadOnlyList<SceneMetadata.SpawnPoint> spawnPoints, Vector3 targetWorldPosition, Vector3 parcelBaseWorldPosition)
        {
            List<SceneMetadata.SpawnPoint> defaults = ListPool<SceneMetadata.SpawnPoint>.Get();
            defaults.AddRange(spawnPoints.Where(sp => sp.@default));

            IReadOnlyList<SceneMetadata.SpawnPoint> elegibleSpawnPoints = defaults.Count > 0 ? defaults : spawnPoints;
            var closestIndex = 0;

            if (elegibleSpawnPoints.Count > 1)
            {
                float closestDistance = float.MaxValue;

                for (var i = 0; i < elegibleSpawnPoints.Count; i++)
                {
                    SceneMetadata.SpawnPoint sp = elegibleSpawnPoints[i];
                    Vector3 spawnWorldPosition = GetSpawnPositionOffset(sp) + parcelBaseWorldPosition;
                    float distance = Vector3.Distance(targetWorldPosition, spawnWorldPosition);

                    if (distance < closestDistance)
                    {
                        closestIndex = i;
                        closestDistance = distance;
                    }
                }
            }

            SceneMetadata.SpawnPoint spawnPoint = elegibleSpawnPoints[closestIndex];

            ListPool<SceneMetadata.SpawnPoint>.Release(defaults);

            return spawnPoint;
        }

        private static Vector3 GetSpawnPositionOffset(SceneMetadata.SpawnPoint spawnPoint)
        {
            static float GetRandomPoint(float[] coordArray)
            {
                float randomPoint = 0;

                switch (coordArray.Length)
                {
                    case 1:
                        randomPoint = coordArray[0];
                        break;
                    case >= 2:
                    {
                        float min = coordArray[0];
                        float max = coordArray[1];

                        if (Mathf.Approximately(min, max))
                            return max;

                        if (min > max)
                            (min, max) = (max, min);

                        randomPoint = (float)((RANDOM.NextDouble() * (max - min)) + min);
                        break;
                    }
                }

                return randomPoint;
            }

            static float? GetSpawnComponent(SceneMetadata.SpawnPoint.Coordinate coordinate)
            {
                if (coordinate.SingleValue != null)
                    return coordinate.SingleValue.Value;

                if (coordinate.MultiValue != null)
                    return GetRandomPoint(coordinate.MultiValue);

                return null;
            }

            return new Vector3(
                GetSpawnComponent(spawnPoint.position.x) ?? ParcelMathHelper.PARCEL_SIZE / 2f,
                GetSpawnComponent(spawnPoint.position.y) ?? 0,
                GetSpawnComponent(spawnPoint.position.z) ?? ParcelMathHelper.PARCEL_SIZE / 2f);
        }
    }
}
