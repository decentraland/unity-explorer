using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Represents the scene with a single runnable JS and many parcels. Corresponds to the `scene.json` file
    /// </summary>
    public interface ISceneData
    {
        [Serializable]
        public struct ContentMappingPair
        {
            public string file;
            public string hash;
        }

        string Name { get; }

        string BaseUrl { get; }

        string BaseUrlBundles { get; }

        IReadOnlyList<Vector2Int> Parcels { get; }

        IReadOnlyList<ContentMappingPair> Contents { get; }

        IReadOnlyList<string> RequiredPermissions { get; }

        IReadOnlyList<string> AllowedMediaHostnames { get; }
    }
}
