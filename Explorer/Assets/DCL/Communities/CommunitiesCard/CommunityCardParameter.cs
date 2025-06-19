using DCL.UI;

namespace DCL.Communities.CommunitiesCard
{
    public struct CommunityCardParameter
    {
        public readonly string CommunityId;
        public readonly ISpriteCache ThumbnailSpriteCache;

        public CommunityCardParameter(string communityId, ISpriteCache spriteCache = null)
        {
            CommunityId = communityId;
            ThumbnailSpriteCache = spriteCache;
        }
    }
}
