using System;

namespace ECS.StreamableLoading.NftShapes.DTOs
{
    [Serializable]
    public struct NftInfoDto
    {
        public NftDto nft;

        public string ImageUrl() =>
            nft.image_url;
    }
}
