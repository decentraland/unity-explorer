using CommunicationData.URLHelpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]

namespace Ipfs
{
    public static class IpfsTypes
    {
        [Serializable]
        public class ContentDefinition
        {
            public string file;
            public string hash;
        }

        [Serializable]
        public class EntityDefinition : EntityDefinitionGeneric<object> { }

        [Serializable]
        public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata> { }

        [Serializable]
        public class EntityDefinitionGeneric<T> : IEquatable<EntityDefinitionGeneric<T>>
        {
            public List<ContentDefinition> content;
            public string id;
            public T metadata;
            public List<string> pointers;

            /// <summary>
            ///     Clear data for the future reusing
            /// </summary>
            internal static void Clear(EntityDefinitionGeneric<T> entityDefinition)
            {
                entityDefinition.content?.Clear();
                entityDefinition.id = string.Empty;
                entityDefinition.pointers?.Clear();
            }

            public bool Equals(EntityDefinitionGeneric<T> other) =>
                id.Equals(other?.id);

            public override string ToString() =>
                id;
        }

        [Serializable]
        [JsonConverter(typeof(SceneParcelsConverter))]
        public class SceneMetadataScene
        {
            [field: NonSerialized]
            public Vector2Int DecodedBase { get; internal set; }

            [field: NonSerialized]
            public IReadOnlyList<Vector2Int> DecodedParcels { get; internal set; }
        }

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

        [Serializable]
        public class ServerConfiguration
        {
            public List<string> scenesUrn;
        }

        [Serializable]
        public class ContentEndpoint
        {
            public bool healthy;
            public string publicUrl;
        }

        [Serializable]
        public class ServerAbout
        {
            public ServerConfiguration configurations;
            public ContentEndpoint content;
            public ContentEndpoint lambdas;

            // public CommsConfig? comms; // TODO for comms
        }

        public readonly struct IpfsPath
        {
            public readonly string EntityId;
            public readonly URLDomain BaseUrl;

            public IpfsPath(string entityId, URLDomain baseUrl)
            {
                EntityId = entityId;
                BaseUrl = baseUrl;
            }

            public URLAddress GetUrl(URLDomain defaultContentUrl)
            {
                var entityAsPath = URLPath.FromString(EntityId);
                return URLBuilder.Combine(!BaseUrl.IsEmpty ? BaseUrl : defaultContentUrl, entityAsPath);
            }

            public override string ToString() =>
                $"IpfsPath (EntityId: {EntityId}, BaseUrl: {BaseUrl.Value})";
        }
    }
}
