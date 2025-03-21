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
        private readonly HashSet<Vector2Int> roadCoordinates;

        public VisualSceneStateResolver(HashSet<Vector2Int> roadCoordinates)
        {
            this.roadCoordinates = roadCoordinates;
        }

        public void ResolveVisualSceneState(ref VisualSceneState visualSceneState, PartitionComponent partition,
            SceneDefinitionComponent sceneDefinitionComponent, ILODSettingsAsset lodSettingsAsset, IRealmData realmData)
        {
            // For PX scenes, we always show the scene
            if (sceneDefinitionComponent.IsPortableExperience) visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;

            //If we are in a world, dont show lods
            if (realmData.ScenesAreFixed) visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;

            //If the scene is empty, no lods are possible
            else if (roadCoordinates.Contains(sceneDefinitionComponent.Definition.metadata.scene.DecodedBase))
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.ROAD;

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
