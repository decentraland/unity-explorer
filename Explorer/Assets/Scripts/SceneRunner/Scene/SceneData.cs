using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     A dummy scene data for local scenes
    /// </summary>
    public class SceneData : ISceneData
    {
        public SceneData(string baseUrl, in RawSceneJson rawSceneJson)
        {
            BaseUrl = baseUrl;
            RequiredPermissions = rawSceneJson.requiredPermissions;
            AllowedMediaHostnames = rawSceneJson.allowedMediaHostnames;
            Name = rawSceneJson.name;
        }

        public SceneData(string jsCodeUrl)
        {
            Name = jsCodeUrl.Substring(jsCodeUrl.LastIndexOf("/") + 1);
            BaseUrl = string.Empty;
        }

        public string Name { get; }
        public string BaseUrl { get; }

        // TODO
        public string BaseUrlBundles { get; }

        // TODO
        public IReadOnlyList<Vector2Int> Parcels { get; } = Array.Empty<Vector2Int>();

        // TODO
        public IReadOnlyList<ISceneData.ContentMappingPair> Contents { get; } = Array.Empty<ISceneData.ContentMappingPair>();
        public IReadOnlyList<string> RequiredPermissions { get; }
        public IReadOnlyList<string> AllowedMediaHostnames { get; }
    }
}
