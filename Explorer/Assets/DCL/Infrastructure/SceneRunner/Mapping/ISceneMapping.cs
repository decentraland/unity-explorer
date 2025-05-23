using System.Collections.Generic;
using Arch.Core;
using UnityEngine;

namespace SceneRunner.Mapping
{
    public interface IReadOnlySceneMapping
    {
        World? GetWorld(string sceneName);

        World? GetWorld(Vector2Int coordinates);
    }

    public interface ISceneMapping : IReadOnlySceneMapping
    {
        void Register(string sceneName, IReadOnlyList<Vector2Int> coordinates, World world);
    }
}
