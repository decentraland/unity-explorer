using System.Collections.Generic;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Landscape.GPUI_Pro;
using GPUInstancerPro.TerrainModule;
using UnityEngine;

public class GPUIWrapper 
{
    public void Initialize(TerrainGenerator terrainGenerator, ref ArchSystemsWorldBuilder<Arch.Core.World> worldBuilder, IDebugContainerBuilder debugBuilder)
    {
        
#if GPUIPRO_PRESENT
        GPUIDebugSystem.InjectToWorld(ref worldBuilder, debugBuilder);
        
        terrainGenerator.GenesisTerrainGenerated += GenesisTerrainGenerated;
        void GenesisTerrainGenerated(List<Terrain> generatedTerrain)
        {
            GameObject gpuiManagers = GameObject.Instantiate(Resources.Load<GameObject>("GPUIPro_Managers"));
            GPUITreeManager treeManager = gpuiManagers.GetComponentInChildren<GPUITreeManager>();
            GPUIDetailManager detailManager = gpuiManagers.GetComponentInChildren<GPUIDetailManager>(); 
            foreach (Terrain terrain in generatedTerrain)
            {
                GPUITerrainAPI.AddTerrain(treeManager, terrain);
                GPUITerrainAPI.AddTerrain(detailManager, terrain);
            }
        }
#endif
        
    }
}
