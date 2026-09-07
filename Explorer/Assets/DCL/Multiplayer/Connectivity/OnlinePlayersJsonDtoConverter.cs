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
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value)
                serializer.Serialize(writer, item);

            writer.WriteEndArray();
        }

        public override List<OnlineUserData>? ReadJson(JsonReader reader, Type objectType, List<OnlineUserData>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            existingValue ??= new List<OnlineUserData>();

            var root = JObject.Load(reader);
            var peers = root["peers"] as JArray;

            if (peers == null)
                return existingValue;

            foreach (JToken peer in peers)
            {
                // JToken's indexer throws on non-object elements (e.g. null)
                if (peer is not JObject peerObject)
                    continue;

                string? address = peerObject["address"]?.Value<string>();
                var posArray = peerObject["position"] as JArray;

                if (address == null || posArray == null || posArray.Count < 3)
                    continue;

                float? x = posArray[0].Value<float?>();
                float? z = posArray[2].Value<float?>();

                if (x == null || z == null)
                    continue;

                // NaN/Infinity would overflow in ToVector3
                if (!float.IsFinite(x.Value) || !float.IsFinite(z.Value))
                    continue;

                existingValue.Add(new OnlineUserData
                {
                    position = ToVector3(x.Value, z.Value),
                    avatarId = address,
                    worldName = WorldNameOf(peerObject["realm"]?.Value<string>()),
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
    }
}
