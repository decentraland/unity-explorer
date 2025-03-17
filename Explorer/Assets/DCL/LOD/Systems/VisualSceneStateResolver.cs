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
        public static HashSet<Vector2Int> roadCoordinates;
        public static ILODSettingsAsset lodSettingsAsset;
        public static IRealmData realmData;

        public static VisualSceneStateEnum ResolveVisualSceneState(PartitionComponent partition, SceneDefinitionComponent sceneDefinitionComponent)
        {
            // For PX scenes, we always show the scene
            if (sceneDefinitionComponent.IsPortableExperience) return VisualSceneStateEnum.SHOWING_SCENE;

            //If we are in a world, dont show lods
            if (realmData.ScenesAreFixed) return VisualSceneStateEnum.SHOWING_SCENE;

            //If the scene is empty, no lods are possible

            if (roadCoordinates.Contains(sceneDefinitionComponent.Definition.metadata.scene.DecodedBase))
                return VisualSceneStateEnum.ROAD;

            //For SDK6 scenes, we just show lod0

            if (!sceneDefinitionComponent.IsSDK7)
                return VisualSceneStateEnum.SHOWING_LOD;

            return partition.Bucket < lodSettingsAsset.SDK7LodThreshold
                ? VisualSceneStateEnum.SHOWING_SCENE
                : VisualSceneStateEnum.SHOWING_LOD;
        }


        public static void ResolveVisualSceneState(ref VisualSceneState visualSceneState, PartitionComponent partition,
            SceneDefinitionComponent sceneDefinitionComponent)
        {
            // For PX scenes, we always show the scene
            if (sceneDefinitionComponent.IsPortableExperience) visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;

            //If we are in a world, dont show lods
            if (realmData.ScenesAreFixed) visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;

            //If the scene is empty, no lods are possible
            else if (roadCoordinates.Contains(sceneDefinitionComponent.Definition.metadata.scene.DecodedBase))
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.ROAD;

            //For SDK6 scenes, we just show lod0
            else if (!sceneDefinitionComponent.IsSDK7)
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;
            else
            {
                var isSceneLoaded = visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE
                    ? lodSettingsAsset.UnloadTolerance
                    : 0;

                //If scene is loaded, the unload distance is bigger to avoid loading/unloadng ping-pong
                var candidateVisualSceneState = partition.Bucket < lodSettingsAsset.SDK7LodThreshold + isSceneLoaded
                    ? VisualSceneStateEnum.SHOWING_SCENE
                    : VisualSceneStateEnum.SHOWING_LOD;

                if (visualSceneState.CurrentVisualSceneState != candidateVisualSceneState)
                {
                    visualSceneState.CurrentVisualSceneState = candidateVisualSceneState;
                    visualSceneState.IsDirty = true;
                }
            }
        }
    }
}
