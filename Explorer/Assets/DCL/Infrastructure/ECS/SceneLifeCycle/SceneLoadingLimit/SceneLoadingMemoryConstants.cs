namespace ECS.SceneLifeCycle.IncreasingRadius
{
    // Following the limits from the docs. One parcel gives 15MB, capped at 300MB. Im considering here the 'worst scenarios' possible
    // https://docs.decentraland.org/creator/development-guide/sdk7/scene-limitations/#scene-limitation-rules
    // We add an estimation factor (1.1f); assets loaded in memory do not have the same size as in disk

    // IE:
    // A single scene can take 330MB
    // A single high quality LOD can take 121MB (MaxSceneSize/3 + MaxSceneSize/30)
    // A single low quality LOD can take 11MB (MaxSceneSize/30)

    // The following values take into consideration the 'worst scenarios', built using GP as reference.
    // Since all scenes dont take do the worst scenario, more will be loaded. This just ensures the upper limit
    public class SceneLoadingMemoryConstants
    {
        private const float RUNTIME_MEMORY_COEFFICENT = 1.1f;
        public const float LOD_REDUCTION = 3;
        public const float QUALITY_REDUCTED_LOD_REDUCTION = 30;

        public const float MAX_SCENE_SIZE = 300 * RUNTIME_MEMORY_COEFFICENT;
        public const float MAX_SCENE_LOD = (MAX_SCENE_SIZE / LOD_REDUCTION) + (MAX_SCENE_SIZE / QUALITY_REDUCTED_LOD_REDUCTION);
        public const float MAX_SCENE_LOWQUALITY_LOD = MAX_SCENE_SIZE / QUALITY_REDUCTED_LOD_REDUCTION;

        public const int LOW_MEMORY_RIG_THRESHOLD = 10_000;
        public const int MEDIUM_MEMORY_RIGH_THRESHOLD = 17_000;
    }
}
