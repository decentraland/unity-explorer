using Decentraland.Kernel.Comms.Rfc4;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    //Struct that will be used to dynamically modify the maximum amount of scenes that can load
    //in the memory system
    public struct SceneLoadingLimit
    {
        public int MaximumAmountOfScenesThatCanLoad;
        public int MaximumAmountOfReductedLoDsThatCanLoad;
        public int MaximumAmountOfLODsThatCanLoad;

        public static SceneLoadingLimit CreateMax() =>
            new ()
            {
                MaximumAmountOfScenesThatCanLoad = int.MaxValue,
                MaximumAmountOfReductedLoDsThatCanLoad = int.MaxValue,
                MaximumAmountOfLODsThatCanLoad = int.MaxValue,
            };
    }
}
