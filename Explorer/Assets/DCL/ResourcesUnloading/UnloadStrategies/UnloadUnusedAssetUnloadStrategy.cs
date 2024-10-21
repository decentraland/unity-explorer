using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class UnloadUnusedAssetUnloadStrategy : UnloadStrategy
    {

        private readonly int FRAMES_UNTIL_UNLOAD_IS_INVOKED = 4_000;
        private int currentFrameCountForUnloadAssets;
        
        public UnloadUnusedAssetUnloadStrategy(UnloadStrategy previousStrategy) : base(previousStrategy)
        {
            //We equalize it so its invoked on first invocation of TryUnload
            currentFrameCountForUnloadAssets = FRAMES_UNTIL_UNLOAD_IS_INVOKED;
        }

        protected override void RunStrategy(ICacheCleaner cacheCleaner)
        {
            currentFrameCountForUnloadAssets++;
            if (currentFrameCountForUnloadAssets > FRAMES_UNTIL_UNLOAD_IS_INVOKED)
            {
                Resources.UnloadUnusedAssets();
                currentFrameCountForUnloadAssets = 0;
            }
        }

        protected override void ResetStrategy()
        {
            currentFrameCountForUnloadAssets = FRAMES_UNTIL_UNLOAD_IS_INVOKED;
        }

    }
}