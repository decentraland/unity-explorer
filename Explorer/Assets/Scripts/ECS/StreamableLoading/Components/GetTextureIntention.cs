using ECS.StreamableLoading.Components.Common;

namespace ECS.StreamableLoading.Components
{
    public struct GetTextureIntention : ILoadingIntention
    {
        public CommonLoadingArguments CommonArguments { get; set; }
        public bool IsReadable;
    }
}
