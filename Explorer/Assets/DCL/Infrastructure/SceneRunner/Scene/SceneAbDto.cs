using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    // this datatype is defined by https://github.com/decentraland/asset-bundle-converter
    [Serializable]
    public struct SceneAbDto
    {
        public const int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        public const int AB_MIN_SUPPORTED_VERSION_MAC = 16;

        [SerializeField]
        internal string version;
        [SerializeField]
        internal string[] files;
        [SerializeField]
        private int exitCode;
        [SerializeField]
        private string date;

        public string Version => version;
        public IReadOnlyList<string> Files => files ?? Array.Empty<string>();

        public string Date => date;
    }
}
