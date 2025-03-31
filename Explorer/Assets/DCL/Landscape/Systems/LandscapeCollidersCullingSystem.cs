using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.Landscape.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [LogCategory(ReportCategory.LANDSCAPE)]
    public partial class LandscapeCollidersCullingSystem : BaseUnityLoopSystem
    {
        private readonly TerrainGenerator terrainGenerator;

        public LandscapeCollidersCullingSystem(World world, TerrainGenerator terrainGenerator) : base(world)
        {
            this.terrainGenerator = terrainGenerator;
        }

        protected override void Update(float t)
        {
            if (!terrainGenerator.IsTerrainShown) return;

        }
    }
}
