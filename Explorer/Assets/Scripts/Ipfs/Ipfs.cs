using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
public class Ipfs
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
        public string[] pointers;
        public ContentDefinition[] content;
        public T metadata;
    }

    public class SceneMetadataScene
    {
        [JsonProperty("base")]
        public string baseParcel;

        public string[] parcels;
    }

    public class SceneMetadata
    {
        public string main;
        public SceneMetadataScene scene;
    }

    public static UnityWebRequestAsyncOperation RequestActiveEntitiesByPointers(string contentBaseUrl, List<Vector2Int> pointers)
    {
        // TODO: Construct directly the string with JSON Format
        List<string> pointerList = pointers.Select(parcel => $"{parcel.x},{parcel.y}").ToList();

        Dictionary<string, object> body = new ()
            { { "pointers", pointerList } };

        string jsonBody = JsonConvert.SerializeObject(body);

        var request = UnityWebRequest.Post(contentBaseUrl + "/entities/active", jsonBody, "application/json");
        return request.SendWebRequest();
    }

    public static Vector2Int DecodePointer(string pointer)
    {
        var commaPosition = pointer.IndexOf(",", StringComparison.Ordinal);
        var span = pointer.AsSpan();

        var firstPart = span[0..commaPosition];
        var secondPart = span[(commaPosition+1)..];

        return new Vector2Int(int.Parse(firstPart), int.Parse(secondPart));
    }
}

