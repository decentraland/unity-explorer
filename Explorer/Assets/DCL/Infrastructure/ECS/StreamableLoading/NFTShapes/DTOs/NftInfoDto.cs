using System;

namespace ECS.StreamableLoading.NFTShapes.DTOs
{
    [Serializable]
    public struct NftInfoDto
    {
        public NftDto nft;

        public Uri ImageUrl() =>
            new (nft.image_url);
    }
}
