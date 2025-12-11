using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public readonly struct CheckCommunityNotificationOptOutResponse
    {
        public readonly string scope;
        public readonly string scopeId;
        public readonly bool optedOut;
    }
}
