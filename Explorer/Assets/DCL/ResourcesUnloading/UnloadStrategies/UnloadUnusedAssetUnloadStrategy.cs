using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class UnloadUnusedAssetUnloadStrategy : UnloadStrategyBase
    {
        private readonly int FRAMES_UNTIL_UNLOAD_IS_INVOKED = 5_000;
        private int currentFrameCountForUnloadAssets;

        public UnloadUnusedAssetUnloadStrategy(int failureThreshold) : base(failureThreshold)
        {
            //We equalize it so its invoked on first invocation of TryUnload
            currentFrameCountForUnloadAssets = FRAMES_UNTIL_UNLOAD_IS_INVOKED;
        }

        public override void RunStrategy()
        {
            currentFrameCountForUnloadAssets++;
            if (currentFrameCountForUnloadAssets > FRAMES_UNTIL_UNLOAD_IS_INVOKED)
            {
                Resources.UnloadUnusedAssets();
                currentFrameCountForUnloadAssets = 0;
            }
        }

        public override void ResetStrategy()
        {
            base.ResetStrategy();
            currentFrameCountForUnloadAssets = FRAMES_UNTIL_UNLOAD_IS_INVOKED;
        }

        
    }
}