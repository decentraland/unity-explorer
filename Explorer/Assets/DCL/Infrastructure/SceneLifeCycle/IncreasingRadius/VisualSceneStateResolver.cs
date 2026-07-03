using DCL.SceneRunner.Scene;
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
            VisualSceneState currentVisualSceneState, bool scenesAreFixed, ISSDescriptor issDescriptor)
        {
            //In worlds, only SDK7 scenes with a resolved ISS descriptor participate in LODs.
            //Anything else keeps the legacy always-full-scene behavior
            if (scenesAreFixed && (!sceneDefinitionComponent.IsSDK7 || !issDescriptor.SupportsDescriptor()))
                return VisualSceneState.SHOWING_SCENE;

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
