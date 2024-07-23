using SceneRunner.Scene;
using System;

namespace ECS.SceneLifeCycle
{
    [Obsolete("Hack: Refactoring required")]
    public class SceneAssetLock
    {
        public ISceneFacade IsLockedBy { get; private set; }

        public bool IsLocked => IsLockedBy != null;

        public void TryUnlock(ISceneFacade scene)
        {
            if (IsLockedBy == scene)
                IsLockedBy = null;
        }

        public void TryLock(ISceneFacade currentScene)
        {
            IsLockedBy = currentScene.IsSceneReady() ? null : currentScene;
        }

        public void Reset()
        {
            IsLockedBy = null;
        }
    }
}
