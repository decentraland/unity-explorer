namespace DCL.Communities.CommunitiesBrowser
{
    public static class CommunitiesBrowserEvents
    {
        public readonly struct UpdateJoinedCommunityEvent
        {
            public readonly string CommunityId;
            public readonly bool Success;
            public readonly bool IsJoined;

            public UpdateJoinedCommunityEvent(string communityId, bool success, bool isJoined)
            {
                CommunityId = communityId;
                Success = success;
                IsJoined = isJoined;
            }
        }

        public readonly struct RequestedToJoinCommunityEvent
        {
            public readonly string CommunityId;

            public RequestedToJoinCommunityEvent(string communityId)
            {
                CommunityId = communityId;
            }
        }

        public readonly struct RequestToJoinCommunityCancelledEvent
        {
            public readonly string CommunityId;
            public readonly string RequestId;

            public RequestToJoinCommunityCancelledEvent(string communityId, string requestId)
            {
                CommunityId = communityId;
                RequestId = requestId;
            }
        }

        public readonly struct CommunityInviteRequestAcceptedEvent
        {
            public readonly string CommunityId;
            public readonly bool Success;

            public CommunityInviteRequestAcceptedEvent(string communityId, bool success)
            {
                CommunityId = communityId;
                Success = success;
            }
        }

        public readonly struct CommunityInviteRequestRejectedEvent
        {
            public readonly string CommunityId;
            public readonly bool Success;

            public CommunityInviteRequestRejectedEvent(string communityId, bool success)
            {
                CommunityId = communityId;
                Success = success;
            }
        }

        public readonly struct CommunityLeftEvent
        {
            public readonly string CommunityId;
            public readonly bool Success;

            public CommunityLeftEvent(string communityId, bool success)
            {
                CommunityId = communityId;
                Success = success;
            }
        }

        public readonly struct CommunityDeletedEvent
        {
            public readonly string CommunityId;

            public CommunityDeletedEvent(string communityId)
            {
                CommunityId = communityId;
            }
        }

        public readonly struct UserRemovedFromCommunityEvent
        {
            public readonly string CommunityId;

            public UserRemovedFromCommunityEvent(string communityId)
            {
                CommunityId = communityId;
            }
        }

        public readonly struct UserBannedFromCommunityEvent
        {
            public readonly string CommunityId;
            public readonly string UserAddress;

            public UserBannedFromCommunityEvent(string communityId, string userAddress)
            {
                CommunityId = communityId;
                UserAddress = userAddress;
            }
        }

        public readonly struct CommunityProfileOpenedEvent
        {
            public readonly string CommunityId;

            public CommunityProfileOpenedEvent(string communityId)
            {
                CommunityId = communityId;
            }
        }

        public readonly struct ClearSearchBarEvent
        {
            // No data needed
        }

        public readonly struct CommunityJoinedClickedEvent
        {
            public readonly string CommunityId;

            public CommunityJoinedClickedEvent(string communityId)
            {
                CommunityId = communityId;
            }
        }
    }
}
