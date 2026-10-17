using System;
using Newtonsoft.Json;

namespace DCL.Backpack.AvatarSection.Outfits.Models
{
    /// <summary>
    ///     Represents a single outfit within the metadata.
    /// </summary>
    [Serializable]
    public class OutfitItem
    {
        [JsonProperty("slot")]
        public int slot;

        [JsonProperty("outfit")]
        public Outfit? outfit;
    }
}