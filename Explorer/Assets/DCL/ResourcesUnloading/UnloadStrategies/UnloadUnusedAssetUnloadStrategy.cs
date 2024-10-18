using UnityEngine;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class UnloadUnusedAssetUnloadStrategy : IUnloadStrategy
    {

        private int FRAMES_UNTIL_UNLOAD_IS_INVOKED = 1000;
        private int currentFrameCountForUnloadAssets;
        
        public UnloadUnusedAssetUnloadStrategy()
        {
            //We equalize it so its invoked on first invocation of TryUnload
            currentFrameCountForUnloadAssets = FRAMES_UNTIL_UNLOAD_IS_INVOKED;
        }

        public override void TryUnload(ICacheCleaner cacheCleaner)
        {
            currentFrameCountForUnloadAssets++;
            if (currentFrameCountForUnloadAssets > FRAMES_UNTIL_UNLOAD_IS_INVOKED)
            {
                Resources.UnloadUnusedAssets();
                currentFrameCountForUnloadAssets = 0;
            }
            base.TryUnload(cacheCleaner);
        }
        
        public override void ResetStrategy()
        {
            base.ResetStrategy();
            currentFrameCountForUnloadAssets = FRAMES_UNTIL_UNLOAD_IS_INVOKED;
        }

    }
}