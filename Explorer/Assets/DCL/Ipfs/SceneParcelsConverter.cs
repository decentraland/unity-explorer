using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Ipfs
{
    /// <summary>
    ///     Converts string representation of parcels into Vector2Int to eliminate the necessity of an additional abstraction layer
    /// </summary>
    public class SceneParcelsConverter : JsonConverter<SceneMetadataScene>
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, SceneMetadataScene value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override SceneMetadataScene ReadJson(JsonReader reader, Type objectType, SceneMetadataScene existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);

            List<string> parcels = jsonObject["parcels"]?.ToObject<List<string>>();
            string baseParcel = jsonObject["base"]?.Value<string>();

            Vector2Int[] decodedParcels;

            if (parcels != null)
            {
                decodedParcels = new Vector2Int[parcels.Count];

                for (var i = 0; i < parcels.Count; i++) { decodedParcels[i] = IpfsHelper.DecodePointer(parcels[i]); }
            }
            else
                decodedParcels = Array.Empty<Vector2Int>();

            return new SceneMetadataScene
            {
                DecodedBase = baseParcel != null ? IpfsHelper.DecodePointer(baseParcel) : Vector2Int.zero,
                DecodedParcels = decodedParcels,
            };
        }
    }
}
