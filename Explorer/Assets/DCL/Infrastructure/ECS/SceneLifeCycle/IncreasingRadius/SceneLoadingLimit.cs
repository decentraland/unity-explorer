using UnityEngine;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public class SceneLoadingLimit
    {
        public int MaximumAmountOfScenesThatCanLoad;
        public int MaximumAmountOfReductedLoDsThatCanLoad;
        public int MaximumAmountOfLODsThatCanLoad;

        public static SceneLoadingLimit CreateMemoryRelativeLimit(int systemMemory)
        {
            //We are talking about 8 GBs Rigs. For now, we only allow two scene loading (to make it usable with scenes with wholes in it),
            //and all LODs are quality reducted
            if (systemMemory < 9_000)
            {
                return new SceneLoadingLimit
                {
                    MaximumAmountOfScenesThatCanLoad = 2,
                    MaximumAmountOfLODsThatCanLoad = 2,
                    MaximumAmountOfReductedLoDsThatCanLoad = int.MaxValue,
                };
            }

            return CreateMax();
        }

        public static SceneLoadingLimit CreateMax() =>
            new ()
            {
                MaximumAmountOfScenesThatCanLoad = int.MaxValue,
                MaximumAmountOfReductedLoDsThatCanLoad = int.MaxValue,
                MaximumAmountOfLODsThatCanLoad = int.MaxValue,
            };
    }
}
