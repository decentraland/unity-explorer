using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace SceneRunner.Scene
{
    public class SceneAssetBundleManifest
    {
        //From v25 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        private const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;

        private readonly string version;
        private readonly HashSet<string> convertedFiles;
        private readonly string buildDate;
        private readonly bool ignoreConvertedFiles;

        public SceneAssetBundleManifest(string version)
        {
            this.version = version;
        }

        public string GetVersion() =>
            version;

        public bool HasHashInPathID() =>
            int.Parse(version.AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;

    }
}
