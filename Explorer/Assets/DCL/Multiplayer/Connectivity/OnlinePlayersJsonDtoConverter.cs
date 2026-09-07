using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace DCL.Multiplayer.Connectivity
{
    [Preserve]
    public class OnlinePlayersJsonDtoConverter : JsonConverter<List<OnlineUserData>>
    {
        private const string WORLD_REALM_SUFFIX = ".dcl.eth";

        public override void WriteJson(JsonWriter writer, List<OnlineUserData>? value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (var item in value)
                serializer.Serialize(writer, item);

            writer.WriteEndArray();
        }

        public override List<OnlineUserData> ReadJson(JsonReader reader, Type objectType, List<OnlineUserData>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            existingValue ??= new List<OnlineUserData>();

            var rootObject = JObject.Load(reader).ToObject<RootObject>();
            foreach (DataObject rootObjectPeer in rootObject.peers)
            {
                existingValue.Add(new OnlineUserData()
                {
                    position = ToVector3(rootObjectPeer.position[0], rootObjectPeer.position[2]),
                    avatarId = rootObjectPeer.address,
                    worldName = WorldNameOf(rootObjectPeer.realm),
                });
            }
            return existingValue;
        }

        /// <summary>
        ///     A peer's realm is either a world's ENS name ("cozyfarm.dcl.eth") or a Genesis City realm
        ///     ("main"): only the former names a world, anything else leaves the world name unset.
        /// </summary>
        private static string? WorldNameOf(string? realm) =>
            realm != null && realm.EndsWith(WORLD_REALM_SUFFIX, StringComparison.OrdinalIgnoreCase)
                ? realm
                : null;

        private static Vector3 ToVector3(float x, float z) =>
            new (Convert.ToInt32(x), 0, Convert.ToInt32(z));

        [Serializable, Preserve]
        private class RootObject
        {
            [Preserve]
            public List<DataObject> peers { get; set; }
        }

        [Serializable, Preserve]
        private class DataObject
        {
            [Preserve]
            public string address { get; set; }
            [Preserve]
            public float[] position { get; set; }
            [Preserve]
            public string? realm { get; set; }
        }
    }
}
