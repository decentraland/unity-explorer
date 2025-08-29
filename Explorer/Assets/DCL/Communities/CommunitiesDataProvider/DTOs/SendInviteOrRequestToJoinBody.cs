using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class SendInviteOrRequestToJoinBody
    {
        public string targetedAddress;
        public string type;
    }
}
