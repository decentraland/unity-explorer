using ECS;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;

namespace DCL.LOD
{
    public class VisualSceneStateResolver
    {
        private const int DEFAULT_UNLOAD_TOLERANCE = 1;
        private const int DEFAULT_SDK7_LOD_THRESHOLD = 2;

        private readonly int unloadTolerance;
        private readonly int sdk7LodThreshold;

        public VisualSceneStateResolver(ILODSettingsAsset? lodSettingsAsset)
        {
            unloadTolerance = lodSettingsAsset?.UnloadTolerance ?? DEFAULT_UNLOAD_TOLERANCE;
            sdk7LodThreshold = lodSettingsAsset?.SDK7LodThreshold ?? DEFAULT_SDK7_LOD_THRESHOLD;
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
