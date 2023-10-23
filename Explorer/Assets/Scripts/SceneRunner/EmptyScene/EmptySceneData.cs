using CommunicationData.URLHelpers;
using Diagnostics;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.EmptyScene
{
    /// <summary>
    ///     Exists in a single instance, shared between all empty scenes
    /// </summary>
    public class EmptySceneData : ISceneData
    {
        public readonly IReadOnlyList<EmptySceneMapping> Mappings;
        private readonly Dictionary<string, string> fileToHash;

        public SceneShortInfo SceneShortInfo { get; }

        public Vector3 BasePosition { get; }

        public SceneAssetBundleManifest AssetBundleManifest => SceneAssetBundleManifest.NULL;
        public StaticSceneMessages StaticSceneMessages => StaticSceneMessages.EMPTY;

        public EmptySceneData(IReadOnlyList<EmptySceneMapping> mappings)
        {
            Mappings = mappings;

            fileToHash = new Dictionary<string, string>(mappings.Count * 2, StringComparer.OrdinalIgnoreCase);

            foreach (EmptySceneMapping mapping in mappings)
            {
                fileToHash[mapping.grass.file] = mapping.grass.hash;
                fileToHash[mapping.environment.file] = mapping.environment.hash;
            }
        }

        public bool HasRequiredPermission(string permission) =>
            throw new NotImplementedException();

        public bool TryGetMainScriptUrl(out URLAddress result) =>
            throw new NotImplementedException();

        public bool TryGetContentUrl(string url, out URLAddress result) =>
            throw new NotImplementedException();

        public bool TryGetHash(string name, out string hash) =>
            fileToHash.TryGetValue(name, out hash);

        public bool TryGetMediaUrl(string url, out URLAddress result) =>
            throw new NotImplementedException();

        public bool IsUrlDomainAllowed(string url) =>
            throw new NotImplementedException();

        public bool IsSdk7() =>
            true;
    }
}
