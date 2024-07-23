using System.Collections.Generic;

namespace SceneRunner.Debugging
{
    public interface IWorldInfo
    {
        string EntityComponentsInfo(int entityId);

        IReadOnlyList<int> EntityIds();
    }
}
