using DCL.Ipfs;
using System;

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

        /// <summary>Present once the LOD lane has published content-addressed LOD names.</summary>
        public SceneAbLodsDto? lods;

        public string Version => version;
        public string Date => date;
    }
}
