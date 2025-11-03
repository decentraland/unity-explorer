using CommunicationData.URLHelpers;
using DCL.WebRequests;

namespace ECS.StreamableLoading.NFTShapes
{
    public struct NftTypeResult
    {
        public readonly WebContentInfo.ContentType Type;
        public readonly URLAddress URL;

        public NftTypeResult(WebContentInfo.ContentType type, URLAddress url)
        {
            Type = type;
            URL = url;
        }
    }
}
