using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Landscape;
using DCL.RealmNavigation;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Character.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class TeleportPositionCalculationSystem : BaseUnityLoopSystem
    {
        private readonly TerrainGenerator terrain;
        private SingleInstanceEntity playerEntity;
        private SingleInstanceEntity cameraEntity;

        public TeleportPositionCalculationSystem(World world, TerrainGenerator terrain) : base(world)
        {
            this.terrain = terrain;
        }

        protected override void Update(float t)
        {
            playerEntity = World.CachePlayer();
            cameraEntity = World.CacheCamera();

            ref PlayerTeleportIntent teleportIntent = ref World.TryGetRef<PlayerTeleportIntent>(playerEntity, out bool hasTeleportIntent);

            if (hasTeleportIntent && teleportIntent.Position == null)
            {
                if (teleportIntent.Position == null)
                {
                    terrain.SetTerrainCollider(teleportIntent.Parcel, true);

                    (Vector3 targetWorldPosition, Vector3? cameraTarget) =
                        TeleportationUtils.PickTargetWithOffset(teleportIntent.SceneDef, teleportIntent.Parcel);

                    teleportIntent.Position = targetWorldPosition;

                    if (cameraTarget != null)
                    {
                        World?.AddOrGet(cameraEntity, new CameraLookAtIntent(cameraTarget.Value, targetWorldPosition));
                        World?.AddOrGet(playerEntity, new PlayerLookAtIntent(cameraTarget.Value, targetWorldPosition));
                    }
                }
            }
        }
    }
}
