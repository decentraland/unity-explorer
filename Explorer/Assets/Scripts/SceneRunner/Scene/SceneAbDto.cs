using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    // this datatype is defined by https://github.com/decentraland/asset-bundle-converter
    [Serializable]
    public struct SceneAbDto
    {
        [SerializeField]
        private string version;
        [SerializeField]
        private string[] files;
        [SerializeField]
        private int exitCode;

        public string Version => version;
        public IReadOnlyList<string> Files => files ?? Array.Empty<string>();
    }
}
