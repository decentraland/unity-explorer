using DCL.WebRequests;
using ECS.StreamableLoading.Textures;
using UnityEngine;

namespace ECS.StreamableLoading.NFTShapes
{
    public static class GetNFTImageIntention
    {
        public static GetTextureIntention Create(string url) =>
            new (url, string.Empty,
                TextureWrapMode.Clamp, FilterMode.Bilinear, TextureType.Albedo, nameof(GetNFTImageIntention), 1);
    }
}
