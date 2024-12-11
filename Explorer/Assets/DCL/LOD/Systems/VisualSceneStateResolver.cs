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
            if (roadCoordinates.Contains(sceneDefinitionComponent.Definition.metadata.scene.DecodedBase))
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.ROAD;
            else
                visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD;
        }
        
    }
}
