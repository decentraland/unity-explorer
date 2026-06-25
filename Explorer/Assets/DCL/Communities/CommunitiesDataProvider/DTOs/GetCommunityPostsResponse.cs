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
        public CommunityPost[] posts = Array.Empty<CommunityPost>();
        public int total;
    }

    [Serializable]
    public class CommunityPost
    {
        public string id = null!;
        public string communityId = null!;
        public string authorAddress = null!;

        // Optional: CommunityPostV2 carries only authorAddress, so these profile fields are absent under v2 — left nullable, no non-null initialization.
        public string? authorName;
        public string? authorProfilePictureUrl;
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
