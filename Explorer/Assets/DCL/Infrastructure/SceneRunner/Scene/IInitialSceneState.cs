using System.Collections.Generic;

namespace DCL.SceneRunner.Scene
{
    public interface IInitialSceneState
    {
        void Dispose();

        HashSet<string> ISSAssets { get; }
    }
}
