using DCL.AssetsProvision;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;

namespace DCL.LOD.Systems
{
    public static class VisualSceneStateUtils
    {
        public static void ResolveVisualSceneState(ref VisualSceneState visualSceneState, PartitionComponent partition,
            SceneDefinitionComponent sceneDefinitionComponent, ProvidedAsset<LODSettingsAsset> lodSettingsAsset)
        {
            //If the scene is empty, no lods are possible
            if (sceneDefinitionComponent.IsEmpty) { visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_SCENE; }

            //For SDK6 scenes, we just show lod0
            else if (!sceneDefinitionComponent.IsSDK7) { visualSceneState.CurrentVisualSceneState = VisualSceneStateEnum.SHOWING_LOD; }
            else
            {
                VisualSceneStateEnum candidateState = partition.Bucket < lodSettingsAsset.Value.LodPartitionBucketThresholds[0]
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
