using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Pool;

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
        public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata>
        {
            public static readonly ObjectPool<SceneEntityDefinition> POOL = new (
                () => new SceneEntityDefinition(),
                actionOnRelease: s =>
                {
                    Clear(s);

                    if (s.metadata != null)
                    {
                        s.metadata.scene.allowedMediaHostnames?.Clear();
                        s.metadata.scene.parcels?.Clear();
                        s.metadata.scene.requiredPermissions?.Clear();
                        s.metadata.scene.@base = string.Empty;
                    }
                },
                defaultCapacity: PoolConstants.SCENES_COUNT,
                maxSize: 1000);
        }

        [Serializable]
        public class EntityDefinitionGeneric<T> : IEquatable<EntityDefinitionGeneric<T>>
        {
            /// <summary>
            ///     Clear data for the future reusing
            /// </summary>
            internal static void Clear(EntityDefinitionGeneric<T> entityDefinition)
            {
                entityDefinition.content?.Clear();
                entityDefinition.id = string.Empty;
                entityDefinition.pointers?.Clear();
            }

            public List<ContentDefinition> content;
            public string id;
            public T metadata;
            public List<string> pointers;

            public bool Equals(EntityDefinitionGeneric<T> other) =>
                id.Equals(other?.id);
        }

        [Serializable]
        public class SceneMetadataScene
        {
            public List<string> allowedMediaHostnames;

            [SerializeField] internal string @base = string.Empty;
            public string baseParcel => @base;

            public List<string> parcels;

            public List<string> requiredPermissions;
        }

        [Serializable]
        public class SceneMetadata
        {
            public string main;
            public SceneMetadataScene scene;
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

            // public CommsConfig? comms; // TODO for comms
        }

        public readonly struct IpfsPath
        {
            public readonly string EntityId;
            public readonly string BaseUrl;

            public IpfsPath(string entityId, string baseUrl)
            {
                EntityId = entityId;
                BaseUrl = baseUrl;
            }

            public string GetUrl(string defaultContentUrl)
            {
                if (BaseUrl.Length > 0)
                    return BaseUrl + EntityId;

                return defaultContentUrl + EntityId;
            }
        }
    }
}
