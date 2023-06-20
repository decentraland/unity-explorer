using Newtonsoft.Json;
using System;
using System.Collections.Generic;

public static class IpfsTypes
{
    public class ContentDefinition
    {
        public string file;
        public string hash;
    }

    public class EntityDefinition : EntityDefinitionGeneric<object> { }

    public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata> { }

    public class EntityDefinitionGeneric<T> : IEquatable<EntityDefinitionGeneric<T>>
    {
        [JsonIgnore]
        public IpfsPath urn;

        public List<ContentDefinition> content;
        public string id;
        public T metadata;
        public List<string> pointers;

        public bool Equals(EntityDefinitionGeneric<T> other) =>
            id.Equals(other?.id);
    }

    public class SceneMetadataScene
    {
        public List<string> allowedMediaHostnames;
        [JsonProperty("base")]
        public string baseParcel;

        public List<string> parcels;

        public List<string> requiredPermissions;
    }

    public class SceneMetadata
    {
        public string main;
        public SceneMetadataScene scene;
    }

    public class ServerConfiguration
    {
        public List<string> scenesUrn;
    }

    public class ContentEndpoint
    {
        public bool healthy;

        public string publicUrl;
    }

    public class ServerAbout
    {
        public ServerConfiguration configurations;
        public ContentEndpoint content;

        // public CommsConfig? comms; // TODO for comms
    }

    public class IpfsPath
    {
        public string Urn;
        public string EntityId;
        public string BaseUrl;

        public string GetUrl(string defaultContentUrl)
        {
            if (BaseUrl.Length > 0)
                return BaseUrl + EntityId;

            return defaultContentUrl + EntityId;
        }
    }
}
