using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    [Serializable]
    public class SceneMetadata
    {
        public string main;
        public SceneMetadataScene scene;
        public string runtimeVersion;
        public List<string> allowedMediaHostnames;
        public List<string> requiredPermissions;
        public List<SpawnPoint>? spawnPoints;

        [Serializable]
        [JsonConverter(typeof(SpawnPointConverter))]
        public struct SpawnPoint
        {
            public string name;

            public bool @default;

            [field: NonSerialized] public SinglePosition? SP { get; internal set; }
            [field: NonSerialized] public MultiPosition? MP { get; internal set; }

            [Serializable]
            public struct SinglePosition
            {
                public float x;
                public float y;
                public float z;
            }

            [Serializable]
            public struct MultiPosition
            {
                public float[] x;
                public float[] y;
                public float[] z;
            }
        }
    }
}
