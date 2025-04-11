using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character;
using DCL.Character.CharacterMotion.Systems;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using UnityEngine;
using Utility;

namespace DCL.Landscape.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [UpdateBefore(typeof(TeleportPositionCalculationSystem))]
    [LogCategory(ReportCategory.LANDSCAPE)]
    public partial class LandscapeCollidersCullingSystem : BaseUnityLoopSystem
    {
        private readonly TerrainGenerator terrain;
        private readonly IScenesCache sceneCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly Entity playerEntity;
        private readonly Transform playerTransform;

        private Vector2Int prevParcel = new (int.MaxValue, int.MaxValue);

        public LandscapeCollidersCullingSystem(World world, TerrainGenerator terrain, IScenesCache sceneCache, ILoadingStatus loadingStatus) : base(world)
        {
            this.terrain = terrain;
            this.sceneCache = sceneCache;
            this.loadingStatus = loadingStatus;

            playerEntity = world.CachePlayer();
            playerTransform = World.Get<CharacterTransform>(playerEntity).Transform;
        }

        protected override void Update(float t)
        {
            // Enable terrain for proper raycasting on teleportation before final position is set
            ref PlayerTeleportIntent teleportIntent = ref World.TryGetRef<PlayerTeleportIntent>(playerEntity, out bool hasTeleportIntent);
            if (hasTeleportIntent && !teleportIntent.IsPositionSet && teleportIntent.SceneDef == null)
            {
                prevParcel = teleportIntent.Parcel;
                terrain.SetTerrainCollider(teleportIntent.Parcel, true);
                return;
            }

            if (terrain.IsTerrainShown && loadingStatus.CurrentStage == LoadingStatus.LoadingStage.Completed)
            {
                Vector2Int newParcel = playerTransform.ParcelPosition();

                if (prevParcel != newParcel)
                {
                    prevParcel = newParcel;
                    terrain.SetTerrainCollider(newParcel, true);
                }
            }
        }
    }
}
