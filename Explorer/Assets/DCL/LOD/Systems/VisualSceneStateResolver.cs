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
            //If we are in a world, dont show lods
            if (realmData.ScenesAreFixed) visualSceneState.CandidateVisualSceneState = visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE;

            else if (roadCoordinates.Contains(sceneDefinitionComponent.Definition.metadata.scene.DecodedBase)) visualSceneState.CandidateVisualSceneState = visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.ROAD;

            //For SDK6 scenes, we just show lod0
            else if (!sceneDefinitionComponent.IsSDK7) visualSceneState.CandidateVisualSceneState = visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;
            else
            {
                var candidateVisualSceneState =  partition.Bucket < lodSettingsAsset.SDK7LodThreshold
                    ? VisualSceneStateEnum.SHOWING_SCENE
                    : VisualSceneStateEnum.SHOWING_LOD;

                if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.UNINITIALIZED)
                {
                    visualSceneState.CandidateVisualSceneState = candidateVisualSceneState;
                    visualSceneState.CurrentVisualSceneState = candidateVisualSceneState;
                    visualSceneState.IsDirty = true;
                }
                else if (visualSceneState.CandidateVisualSceneState != candidateVisualSceneState &&
                         candidateVisualSceneState != visualSceneState.CurrentVisualSceneState)
                {
                    visualSceneState.CandidateVisualSceneState = candidateVisualSceneState;
                    visualSceneState.IsDirty = true;
                    visualSceneState.TimeToChange = 0;
                }
            }
        }
    }
}
