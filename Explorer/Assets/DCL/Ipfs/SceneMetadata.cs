using DCL.Utilities.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Ipfs
{
    [Serializable]
    public class SceneMetadata : PreserveOriginalJson
    {
        public string main;
        public SceneMetadataScene scene;
        public string runtimeVersion;
        public List<string> allowedMediaHostnames;
        public List<string> requiredPermissions;
        public List<SpawnPoint>? spawnPoints;

        [Serializable]
        public struct SpawnPoint
        {
            public string name;

            public bool @default;

            public Position position;
            public Position? cameraTarget;

            [Serializable]
            public struct Position
            {
                public Coordinate x;
                public Coordinate y;
                public Coordinate z;

                public Vector3 ToVector3() =>
                    new (x.SingleValue ?? 0f, y.SingleValue ?? 0f, z.SingleValue ?? 0f);
            }

            /// <summary>
            /// Coordinates is either a single value or a list of values
            /// </summary>
            [JsonConverter(typeof(SpawnPointCoordinateConverter))]
            public struct Coordinate
            {
                public float? SingleValue { get; internal set; }
                public float[]? MultiValue { get; internal set; }
            }
        }
    }
}
