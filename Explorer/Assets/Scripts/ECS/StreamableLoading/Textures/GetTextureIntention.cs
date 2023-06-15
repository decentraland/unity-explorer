using ECS.StreamableLoading.Common.Components;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    public struct GetTextureIntention : ILoadingIntention
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public bool IsReadable;
        public TextureWrapMode WrapMode;
        public FilterMode FilterMode;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.cancellationTokenSource;
    }
}
