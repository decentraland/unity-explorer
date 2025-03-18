using System.Collections.Generic;
using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace DCL.LOD
{
    public class VisualSceneStateResolver
    {
        public static ILODSettingsAsset lodSettingsAsset;
        public static IRealmData realmData;

        public static VisualSceneStateEnum ResolveVisualSceneState(PartitionComponent partition, SceneDefinitionComponent sceneDefinitionComponent, VisualSceneStateEnum currentVisualSceneState)
        {
            // For PX scenes, we always show the scene
            if (sceneDefinitionComponent.IsPortableExperience) return VisualSceneStateEnum.SHOWING_SCENE;

            //If we are in a world, dont show lods
            if (realmData.ScenesAreFixed) return VisualSceneStateEnum.SHOWING_SCENE;

            //For SDK6 scenes, we just show lod0
            if (!sceneDefinitionComponent.IsSDK7)
                return VisualSceneStateEnum.SHOWING_LOD;

            int isSceneLoaded = currentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE
                ? lodSettingsAsset.UnloadTolerance
                : 0;

            return partition.Bucket < lodSettingsAsset.SDK7LodThreshold + isSceneLoaded
                ? VisualSceneStateEnum.SHOWING_SCENE
                : VisualSceneStateEnum.SHOWING_LOD;
        }

    }
}
