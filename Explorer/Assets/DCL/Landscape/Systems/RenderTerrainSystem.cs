using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using Decentraland.Terrain;
using ECS.Abstract;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(UpdateCinemachineBrainSystem))]
    public sealed partial class RenderTerrainSystem : BaseUnityLoopSystem
    {
        private readonly TerrainData terrainData;

        private RenderTerrainSystem(World world, TerrainData terrainData)
            : base(world)
        {
            this.terrainData = terrainData;
        }

        protected override void Update(float t) =>
            RenderTerrainQuery(World);

        [Query]
        private void RenderTerrain(ICinemachinePreset cinemachinePreset)
        {
            TerrainRenderer.Render(terrainData, cinemachinePreset.Brain.OutputCamera,
#if UNITY_EDITOR
                true
#else
                false
#endif
            );
        }
    }
}
