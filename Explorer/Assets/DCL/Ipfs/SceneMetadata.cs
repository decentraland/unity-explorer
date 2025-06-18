using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

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
        public bool isPortableExperience;
        public WorldConfiguration? worldConfiguration;
        public SkyboxConfigData? skyboxConfig;

        /// <summary>
        /// Configuration specific to Decentraland Worlds (Realms).
        /// From the scene.json file, at the root level:
        /// {
        ///     ...
        ///     "worldConfiguration": {
        ///         "name": "my-name.dcl.eth",
        ///         "skyboxConfig": {
        ///             "fixedTimeOfDay": 36000
        ///         },
        ///         "fixedAdapter": "offline:offline"
        ///     }
        ///     ...
        /// }
        ///
        /// skyboxConfig.fixedTimeOfDay: This property indicates how many
        /// seconds have passed (in Decentraland time) since the start of the day,
        /// assuming the full cycle lasts 24 hours. Divide the seconds' value by 60 to get minutes,
        /// and by 60 again to get the hours since the start of the day.
        /// For example, if the seconds' value is 36000, it corresponds to 10:00 am.
        /// If no value is set for this field, the world will follow the same day/night cycle as Genesis City.
        ///
        /// NOTE: more about the setup: https://docs.decentraland.org/creator/worlds/about/
        /// </summary>
        [Serializable]
        public struct WorldConfiguration
        {
            /// <summary>
            /// The unique name of the world (e.g., an ENS name).
            /// </summary>
            public string? Name { get; set; }

            /// <summary>
            /// Configuration for the world's skybox.
            /// This config is no longer limited to worlds, so now it's supported at the scene level config, but it's maintained for backwards compatibility.
            /// </summary>
            public SkyboxConfigData? SkyboxConfig { get; set; }

            /// <summary>
            /// Specifies the adapter used for scene deployment or loading (e.g., "offline:offline").
            /// </summary>
            public string? FixedAdapter { get; set; }
        }

        /// <summary>
        /// Configuration works for Decentraland Worlds (Realms) and Genesis Scenes.
        /// From the scene.json file, at the root level:
        /// {
        ///     ...
        ///     "skyboxConfig": {
        ///         "fixedTimeOfDay": 36000
        ///     }
        ///     ...
        /// }
        ///
        /// skyboxConfig.fixedTimeOfDay: This property indicates how many
        /// seconds have passed (in Decentraland time) since the start of the day,
        /// assuming the full cycle lasts 24 hours. Divide the seconds' value by 60 to get minutes,
        /// and by 60 again to get the hours since the start of the day.
        /// For example, if the seconds' value is 36000, it corresponds to 10:00 am.
        /// If no value is set for this field, the world will follow the same day/night cycle as Genesis City.
        ///
        /// NOTE: more about the setup: https://docs.decentraland.org/creator/worlds/about/
        /// </summary>
        [Serializable]
        public struct SkyboxConfigData
        {
            public float fixedTimeOfDay;
        }

        [JsonIgnore]
        public string OriginalJson { get; set; }

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
            ///     Coordinates is either a single value or a list of values
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
