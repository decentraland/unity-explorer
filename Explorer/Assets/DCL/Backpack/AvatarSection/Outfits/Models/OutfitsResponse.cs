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

        [JsonProperty("content")]
        public object[] Content;

        [JsonProperty("metadata")]
        public OutfitsMetadata? Metadata;
    }
}