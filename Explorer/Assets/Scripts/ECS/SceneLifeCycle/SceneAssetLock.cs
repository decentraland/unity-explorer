using JetBrains.Annotations;
using SceneRunner.Scene;
using System;

namespace ECS.SceneLifeCycle
{
    [Obsolete("Refactoring required.")]
    public class SceneAssetLock
    {
        [CanBeNull] public ISceneFacade IsLockedBy;
    }
}
