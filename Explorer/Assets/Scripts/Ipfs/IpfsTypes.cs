using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
public static class IpfsTypes
{
    public class ContentDefinition
    {
        public string file;
        public string hash;
    }

    public class EntityDefinition : EntityDefinitionGeneric<object> {}

    public class SceneEntityDefinition : EntityDefinitionGeneric<SceneMetadata> {}

    public class EntityDefinitionGeneric<T>
    {
        public string id;
        public List<string> pointers;
        public List<ContentDefinition> content;
        public T metadata;
    }

    public class SceneMetadataScene
    {
        [JsonProperty("base")]
        public string baseParcel;

        public List<string> parcels;
    }

    public class SceneMetadata
    {
        public string main;
        public SceneMetadataScene scene;
    }
}

