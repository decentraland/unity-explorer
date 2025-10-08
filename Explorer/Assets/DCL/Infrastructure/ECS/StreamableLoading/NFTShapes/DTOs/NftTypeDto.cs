using CommunicationData.URLHelpers;
using DCL.WebRequests;

namespace ECS.StreamableLoading.NFTShapes.DTOs
{
    public struct NftTypeDto
    {
        public WebContentInfo.ContentType Type;
        public URLAddress URL;
    }
}
