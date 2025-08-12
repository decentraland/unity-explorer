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
        public string version;
        public string[] files;
        public int exitCode;
        public string date;

        public string Version => version;
        public string Date => date;
    }
}
