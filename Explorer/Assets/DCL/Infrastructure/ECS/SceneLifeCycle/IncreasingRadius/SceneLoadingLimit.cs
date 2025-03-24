namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public class SceneLoadingLimit
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
