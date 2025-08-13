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
        private readonly TerrainRendererState state;

        private RenderTerrainSystem(World world, TerrainData terrainData,
            GrassIndirectRenderer grassIndirectRenderer) : base(world)
        {
            this.grassIndirectRenderer = grassIndirectRenderer;
            state = new TerrainRendererState(terrainData);
            state.RenderDetailIndirect = true;
        }

        protected override void Update(float t) =>
            RenderTerrainQuery(World);

        [Query]
        private void RenderTerrain(ICinemachinePreset cinemachinePreset)
        {
            Camera camera = cinemachinePreset.Brain.OutputCamera;

#if UNITY_EDITOR
            state.RenderToAllCameras = true;
#else
            state.RenderToAllCameras = false;
#endif

            if (state.RenderDetailIndirect)
                grassIndirectRenderer.Render(state.TerrainData, camera, state.RenderToAllCameras);

            TerrainRenderer.Render(state, camera);
        }
    }
}
