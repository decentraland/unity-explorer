using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public struct CheckCommunityNotificationOptOutResponse
    {
        public string scope;
        public string scopeId;
        public bool optedOut;
    }
}
