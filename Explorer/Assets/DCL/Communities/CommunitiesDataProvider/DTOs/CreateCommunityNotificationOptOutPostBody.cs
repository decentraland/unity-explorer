using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public struct CreateCommunityNotificationOptOutPostBody
    {
        public string scope;
        public string scopeId;
    }
}
