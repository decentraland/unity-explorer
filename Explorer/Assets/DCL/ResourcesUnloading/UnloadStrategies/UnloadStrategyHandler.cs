using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class UnloadStrategyHandler
    {
        internal UnloadStrategyBase[] unloadStrategies;
        private readonly int DEFAULT_FRAME_FAILURE_THRESHOLD = 250;
        private UnloadStrategyState unloadStrategyState;


        public UnloadStrategyHandler(ICacheCleaner cacheCleaner)
        {

            //The base strategy at 0 will always run
            //On top of that, we adds logic that run only if the previous one fails in an additive manner
            unloadStrategies = new UnloadStrategyBase[]
            {
                new StandardUnloadStrategy(DEFAULT_FRAME_FAILURE_THRESHOLD, cacheCleaner),
                new UnloadUnusedAssetUnloadStrategy(DEFAULT_FRAME_FAILURE_THRESHOLD)
            };

            unloadStrategyState = UnloadStrategyState.Normal;
        }

        private void TryUnload()
        {
            Debug.Log("JUANI RUNNING STRATEGY");
            for (var i = unloadStrategies.Length - 1; i >= 0; i--)
            {
                if (i == 0 || unloadStrategies[i - 1].FaillingOverThreshold())
                    unloadStrategies[i].TryUnload();
            }
        }

        private void ResetToNormal()
        {
            for (var i = 0; i < unloadStrategies.Length; i++)
                unloadStrategies[i].ResetStrategy();
        }

        public void ReportMemoryState(bool isMemoryNormal, bool isInAbundance)
        {
            switch (unloadStrategyState)
            {
                case UnloadStrategyState.Normal:
                    if (!isMemoryNormal)
                    {
                        unloadStrategyState = UnloadStrategyState.Unloading;
                        TryUnload();
                    }
                    break;
                case UnloadStrategyState.Unloading:
                    if (isInAbundance)
                    {
                        ResetToNormal();
                        unloadStrategyState = UnloadStrategyState.Normal;
                    }
                    else
                        TryUnload();
                    break;
            }
        }

        private enum UnloadStrategyState
        {
            Normal,
            Unloading
        }

    }
}
