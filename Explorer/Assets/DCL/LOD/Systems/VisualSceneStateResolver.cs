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
        private HashSet<Vector2Int> roadCoordinates;

        public void SetRoadCoordinates(HashSet<Vector2Int> roadCoordinates)
        {
            this.roadCoordinates = roadCoordinates;
        }

        public void ResolveVisualSceneState(ref VisualSceneState visualSceneState, PartitionComponent partition,
            SceneDefinitionComponent sceneDefinitionComponent, ILODSettingsAsset lodSettingsAsset, IRealmData realmData)
        {
            //If we are in a world, dont show lods
            if (realmData.ScenesAreFixed)
            {
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;
            }
            //If the scene is empty, no lods are possible
            else if (sceneDefinitionComponent.IsEmpty) { visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE; }
            else if(roadCoordinates.Contains(sceneDefinitionComponent.Definition.metadata.scene.DecodedBase)){ visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.ROAD;  }
            //For SDK6 scenes, we just show lod0
            else if (!sceneDefinitionComponent.IsSDK7) { visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD; }
            else
            {
                var candidateState = partition.Bucket < lodSettingsAsset.LodPartitionBucketThresholds[0]
                    ? VisualSceneStateEnum.SHOWING_SCENE
                    : VisualSceneStateEnum.SHOWING_LOD;

                if (candidateState != visualSceneState.CurrentVisualSceneState)
                {
                    visualSceneState.CurrentVisualSceneState = candidateState;
                    visualSceneState.IsDirty = true;
                }
            }
        }
    }
}