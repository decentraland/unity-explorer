using UnityEngine;

namespace SceneRunner.Debugging.Hub
{
    public interface IWorldInfoHub
    {
        IWorldInfo? WorldInfo(string sceneName);

        IWorldInfo? WorldInfo(Vector2Int coordinates);
    }
}
