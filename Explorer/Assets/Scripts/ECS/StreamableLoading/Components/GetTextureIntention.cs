using ECS.StreamableLoading.Components.Common;
using UnityEngine;

namespace ECS.StreamableLoading.Components
{
    public struct GetTextureIntention : ILoadingIntention
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public bool IsReadable;
        public TextureWrapMode WrapMode;
        public FilterMode FilterMode;
    }
}
