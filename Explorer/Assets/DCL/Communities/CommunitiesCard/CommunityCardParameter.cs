using DCL.UI;

namespace DCL.Communities.CommunitiesCard
{
    public struct CommunityCardParameter
    {
        public readonly string CommunityId;

        /// <summary>
        /// The cache where the community card view will find the already downloaded textures. If null, the view will use its own.
        /// </summary>
        public readonly ISpriteCache ThumbnailSpriteCache;

        public CommunityCardParameter(string communityId, ISpriteCache spriteCache = null)
        {
            CommunityId = communityId;
            ThumbnailSpriteCache = spriteCache;
        }
    }
}
