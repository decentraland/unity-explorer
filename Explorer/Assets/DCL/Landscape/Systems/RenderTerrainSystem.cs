using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using Decentraland.Terrain;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(UpdateCinemachineBrainSystem))]
    public sealed partial class RenderTerrainSystem : BaseUnityLoopSystem
    {
        private readonly GrassIndirectRenderer grassIndirectRenderer;
        private readonly TerrainData terrainData;

        private RenderTerrainSystem(World world, TerrainData terrainData,
            GrassIndirectRenderer grassIndirectRenderer) : base(world)
        {
            this.grassIndirectRenderer = grassIndirectRenderer;
            this.terrainData = terrainData;
        }

        protected override void Update(float t) =>
            RenderTerrainQuery(World);

        [Query]
        private void RenderTerrain(ICinemachinePreset cinemachinePreset)
        {
            Camera camera = cinemachinePreset.Brain.OutputCamera;

#if UNITY_EDITOR
            bool renderToAllCameras = true;
#else
            bool renderToAllCameras = false;
#endif

            grassIndirectRenderer.Render(terrainData, camera, renderToAllCameras);
            TerrainRenderer.Render(terrainData, camera, renderToAllCameras, true);
        }
    }
}
