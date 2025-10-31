using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public struct GetCommunityPostsResponse
    {
        public CommunityPostsData data;
    }

    [Serializable]
    public struct CommunityPostsData
    {
        public CommunityPost[] posts;
        public int total;
        public int limit;
        public int offset;
    }

    [Serializable]
    public struct CommunityPost
    {
        public string id;
        public string communityId;
        public string authorAddress;
        public string authorName;
        public string authorProfilePictureUrl;
        public bool authorHasClaimedName;
        public string content;
        public string createdAt;
        public int likesCount;
        public bool isLikedByUser;
    }
}
