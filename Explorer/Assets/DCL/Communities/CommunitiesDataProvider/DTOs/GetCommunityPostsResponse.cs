using DCL.Profiles;
using Newtonsoft.Json;
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

        // Hydrated client-side from authorAddress; CommunityPostV2 sends only the address.
        [JsonIgnore] public Profile.CompactInfo Profile { get; internal set; }

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
