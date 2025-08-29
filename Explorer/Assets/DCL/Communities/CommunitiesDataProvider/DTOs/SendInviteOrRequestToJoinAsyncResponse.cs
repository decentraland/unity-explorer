using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class SendInviteOrRequestToJoinAsyncResponse
    {
        [Serializable]
        public struct InviteOrRequestData
        {
            public string id;
            public string communityId;
            public InviteRequestAction type;
            public string status;
        }

        public InviteOrRequestData data;
    }
}
