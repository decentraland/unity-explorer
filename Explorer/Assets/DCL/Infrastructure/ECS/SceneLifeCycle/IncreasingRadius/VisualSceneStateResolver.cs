using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;

namespace DCL.LOD
{
    public class VisualSceneStateResolver
    {
        private readonly int unloadTolerance;
        private readonly int sdk7LodThreshold;

        public VisualSceneStateResolver(ILODSettingsAsset lodSettingsAsset)
        {
            unloadTolerance = lodSettingsAsset.UnloadTolerance;
            sdk7LodThreshold = lodSettingsAsset.SDK7LodThreshold;
        }

        public VisualSceneState ResolveVisualSceneState(PartitionComponent partition, SceneDefinitionComponent sceneDefinitionComponent,
            VisualSceneState currentVisualSceneState, bool scenesAreFixed)
        {
            //If we are in a world, dont show lods
            if (scenesAreFixed) return VisualSceneState.SHOWING_SCENE;

            //For SDK6 scenes, we just show lod0
            if (!sceneDefinitionComponent.IsSDK7)
                return VisualSceneState.SHOWING_LOD;

            int isSceneLoaded = currentVisualSceneState == VisualSceneState.SHOWING_SCENE
                ? unloadTolerance
                : 0;

            return partition.Bucket < sdk7LodThreshold + isSceneLoaded
                ? VisualSceneState.SHOWING_SCENE
                : VisualSceneState.SHOWING_LOD;
        }

    }
}
