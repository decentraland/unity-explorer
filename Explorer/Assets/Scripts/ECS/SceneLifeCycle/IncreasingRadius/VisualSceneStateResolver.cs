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

        public VisualSceneStateEnum ResolveVisualSceneState(PartitionComponent partition, SceneDefinitionComponent sceneDefinitionComponent,
            VisualSceneStateEnum currentVisualSceneState, bool scenesAreFixed)
        {
            //If we are in a world, dont show lods
            if (scenesAreFixed) return VisualSceneStateEnum.SHOWING_SCENE;

            //For SDK6 scenes, we just show lod0
            if (!sceneDefinitionComponent.IsSDK7)
                return VisualSceneStateEnum.SHOWING_LOD;

            int isSceneLoaded = currentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE
                ? unloadTolerance
                : 0;

            return partition.Bucket < sdk7LodThreshold + isSceneLoaded
                ? VisualSceneStateEnum.SHOWING_SCENE
                : VisualSceneStateEnum.SHOWING_LOD;
        }

    }
}
