using System;
using Newtonsoft.Json;

namespace DCL.Backpack.AvatarSection.Outfits.Models
{
    [Serializable]
    public class OutfitsResponse
    {
        [JsonProperty("version")]
        public string Version;

        [JsonProperty("id")]
        public string Id;

        [JsonProperty("type")]
        public string Type;

        [JsonProperty("timestamp")]
        public long Timestamp;

        [JsonProperty("pointers")]
        public string[] Pointers;

        // Newtonsoft-deserialized wire DTO (CreateFromJson with WRJsonParser.Newtonsoft); Unity serialization never sees this field.
#pragma warning disable UAC1001
        [JsonProperty("content")]
        public object[] Content;
#pragma warning restore UAC1001

        [JsonProperty("metadata")]
        public OutfitsMetadata? Metadata;
    }
}