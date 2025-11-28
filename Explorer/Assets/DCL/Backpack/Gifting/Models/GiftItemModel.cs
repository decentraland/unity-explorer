using System;
using System.Collections.Generic;

namespace DCL.Backpack.Gifting.Models
{
    /// <summary>
    /// The clean domain model used by the UI
    /// </summary>
    public struct GiftItemModel
    {
        public string Name;
        public string Description;
        public string ImageUrl;
        public string Rarity;
        public string Category;
    }

    /// <summary>
    /// The raw JSON response DTO from the Lambda
    /// </summary>
    [Serializable]
    public class GiftItemResponseDTO
    {
        public string id;
        public string name;
        public string description;
        public string thumbnail;
        public string image;
        public List<GiftItemAttributeDTO> attributes;
    }

    [Serializable]
    public class GiftItemAttributeDTO
    {
        public string trait_type;
        public string value;
    }
}