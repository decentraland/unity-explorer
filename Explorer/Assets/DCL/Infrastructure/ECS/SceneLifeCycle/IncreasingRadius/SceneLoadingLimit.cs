namespace ECS.SceneLifeCycle.IncreasingRadius
{
    //Struct that will be used to dynamically modify the maximum amount of scenes that can load
    //in the memory system
    public struct SceneLoadingLimit
    {
        public readonly int MaximumAmountOfScenesThatCanLoad;
        public readonly int MaximumAmountOfReductedLoDsThatCanLoad;
        public readonly int MaximumAmoutOfLODsThatCanLoad;

        public SceneLoadingLimit(int maximumAmoutOfLoDsThatCanLoad, int maximumAmountOfScenesThatCanLoad, int maximumAmountOfReductedLoDsThatCanLoad)
        {
            MaximumAmoutOfLODsThatCanLoad = int.MaxValue;
            MaximumAmountOfScenesThatCanLoad = int.MaxValue;
            MaximumAmountOfReductedLoDsThatCanLoad = int.MaxValue;
        }
    }
}
