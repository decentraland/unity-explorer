using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character;
using DCL.Character.Components;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using UnityEngine;
using Utility;

namespace DCL.Landscape.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [LogCategory(ReportCategory.LANDSCAPE)]
    public partial class LandscapeCollidersCullingSystem : BaseUnityLoopSystem
    {
        private readonly TerrainGenerator terrainGenerator;
        // private readonly IScenesCache sceneCache;
        private readonly Entity playerEntity;

        public LandscapeCollidersCullingSystem(World world, TerrainGenerator terrainGenerator) : base(world)
        {
            this.terrainGenerator = terrainGenerator;
            // this.sceneCache = sceneCache;
            playerEntity = world.CachePlayer();
        }

        protected override void Update(float t)
        {
            if (terrainGenerator.IsTerrainShown)
            {
                var enableTerrainCollider = true;
                // sceneCache.CurrentScene == null;

                var position = World.Get<CharacterTransform>(playerEntity).Transform.ParcelPosition();
                terrainGenerator.SetTerrainCollider(position, true);
            };
        }
    }
}
