using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using System.ComponentModel;

namespace DCL.Ipfs
{
    [Serializable]
    public class SceneMetadata
    {
        public string main;
        public SceneMetadataScene scene;
        public string runtimeVersion;
        public string sdkVersion;
        public List<string> allowedMediaHostnames;
        public List<string> requiredPermissions;
        public List<SpawnPoint>? spawnPoints;
        public bool isPortableExperience;
        public WorldConfiguration? worldConfiguration;
        public SkyboxConfigData? skyboxConfig;
        public FeatureToggles featureToggles;

        /// <summary>
        /// Configuration specific to Decentraland Worlds (Realms).
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
            /// Defines settings for the world's skybox.
            /// This config is no longer limited to worlds, so now it's supported at the scene level config, but it's maintained here for backwards compatibility.
            /// </summary>
            public SkyboxConfigData? SkyboxConfig { get; set; }

            /// <summary>
            /// Specifies the adapter used for scene deployment or loading (e.g., "offline:offline").
            /// </summary>
            public string? FixedAdapter { get; set; }
        }

        /// <summary>
        /// Defines settings for the world's skybox.
        /// </summary>
        [Serializable]
        public struct SkyboxConfigData
        {
            public float? fixedTime;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            [DefaultValue(TransitionMode.FORWARD)]
            public TransitionMode transitionMode;
        }

        public enum TransitionMode
        {
            FORWARD,
            BACKWARD,
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

        [Serializable]
        public struct FeatureToggles
        {
            private const string PORTABLE_EXPERIENCES_ENABLED = "enabled";
            private const string PORTABLE_EXPERIENCES_DISABLED = "disabled";
            private const string PORTABLE_EXPERIENCES_HIDE_UI = "hideUi";

            [SerializeField]
            [JsonProperty]
            private string portableExperiences;

            public bool PortableExperiencesEnabled => string.IsNullOrEmpty(portableExperiences) ||
                                                      string.Equals(portableExperiences, PORTABLE_EXPERIENCES_ENABLED, StringComparison.OrdinalIgnoreCase) ||
                                                      string.Equals(portableExperiences, PORTABLE_EXPERIENCES_HIDE_UI, StringComparison.OrdinalIgnoreCase);
        }
    }
}
