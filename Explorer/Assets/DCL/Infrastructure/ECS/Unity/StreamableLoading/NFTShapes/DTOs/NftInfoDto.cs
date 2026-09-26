using System;

namespace ECS.StreamableLoading.NFTShapes.DTOs
{
    [Serializable]
    public struct NftInfoDto
    {
        public NftDto nft;

        public string ImageUrl() =>
            nft.image_url;
    }
}
