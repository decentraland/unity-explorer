using Arch.Core;
using Arch.SystemGroups;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Systems;
using UnityEngine;
using Utility;

namespace DCL.Landscape.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(UpdateCurrentSceneSystem))]
    [LogCategory(ReportCategory.LANDSCAPE)]
    public partial class LandscapeCollidersCullingSystem : BaseUnityLoopSystem
    {
        private readonly TerrainGenerator terrainGenerator;
        private readonly IScenesCache sceneCache;
        private readonly ILoadingStatus loadingStatus;
        private readonly Entity playerEntity;

        private Vector2Int prevParcel = new (int.MaxValue, int.MaxValue);

        public LandscapeCollidersCullingSystem(World world, TerrainGenerator terrainGenerator, IScenesCache sceneCache, ILoadingStatus loadingStatus) : base(world)
        {
            this.terrainGenerator = terrainGenerator;
            this.sceneCache = sceneCache;
            this.loadingStatus = loadingStatus;
            playerEntity = world.CachePlayer();
        }

        protected override void Update(float t)
        {
            if (!terrainGenerator.IsTerrainShown || loadingStatus.CurrentStage != LoadingStatus.LoadingStage.Completed) return;

            Vector2Int newParcel = World.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();

            if (prevParcel == newParcel)
                return;

            prevParcel = newParcel;

            bool enableTerrainCollider = sceneCache.CurrentScene == null || sceneCache.CurrentScene.Contains(newParcel);

            terrainGenerator.SetTerrainCollider(newParcel, enableTerrainCollider);
        }
    }
}
