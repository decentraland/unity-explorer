using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    // Server schema: social-service-ea docs/schemas.yaml#/components/schemas/GetCommunityPostsV2200OkResponse (post items: CommunityPostV2).
    [Serializable]
    public class GetCommunityPostsResponse
    {
        public CommunityPostsData data = null!;
    }

    [Serializable]
    public class CommunityPostsData
    {
        public CommunityPost[] posts = null!;
        public int total;
    }

    [Serializable]
    public class CommunityPost
    {
        public string id = null!;
        public string communityId = null!;
        public string authorAddress = null!;
        public string authorName;
        public string authorProfilePictureUrl;
        public bool authorHasClaimedName;
        public string content = null!;
        public string createdAt = null!;
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
