using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetCommunityPostsResponse
    {
        public CommunityPostsData data;
    }

    [Serializable]
    public class CommunityPostsData
    {
        public CommunityPost[] posts;
        public int total;
    }

    [Serializable]
    public class CommunityPost
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
        public CommunityPostType type = CommunityPostType.POST;
    }

    public enum CommunityPostType
    {
        POST,
        CREATION_INPUT,
        SEPARATOR,
    }
}
