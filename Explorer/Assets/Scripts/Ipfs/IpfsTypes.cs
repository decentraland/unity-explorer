using Newtonsoft.Json;
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

    public class EntityDefinitionGeneric<T>
    {
        public List<ContentDefinition> content;
        public string id;
        public T metadata;
        public List<string> pointers;
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
}
