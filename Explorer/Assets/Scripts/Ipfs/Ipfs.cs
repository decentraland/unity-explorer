using Newtonsoft.Json;
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

    public class EntityDefinition
    {
        public string id;
        public string[] pointers;
        public ContentDefinition[] content;
        public object metadata;
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

    public static UnityWebRequestAsyncOperation RequestActiveEntities(string contentBaseUrl, List<Vector2Int> pointers)
    {
        List<string> pointerList = pointers.Select(parcel => $"{parcel.x},{parcel.y}").ToList();

        Dictionary<string, object> body = new ()
            { { "pointers", pointerList } };

        string jsonBody = JsonConvert.SerializeObject(body);

        var request = UnityWebRequest.Post(contentBaseUrl + "/entities/active", jsonBody, "application/json");
        return request.SendWebRequest();
    }

    public static UnityWebRequestAsyncOperation RequestContentFile(string contentBaseUrl, ContentDefinition[] contentDefinitions, string file)
    {
        var content = contentDefinitions.First(definition => definition.file == file);
        var request = UnityWebRequest.Get(contentBaseUrl + "/contents/" + content.hash);
        request.SetRequestHeader("Content-Type", "application/json");
        return request.SendWebRequest();
    }
}

