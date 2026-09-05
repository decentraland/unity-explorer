using DCL.UI;

namespace DCL.Communities.CommunityCreation
{
    public struct CommunityCreationEditionParameter
    {
        /// <summary>
        ///     Used to activate either the splash screen to buy a NAME or the community creation/edition flow.
        /// </summary>
        public readonly bool CanCreateCommunities;
        /// <summary>
        ///     The community ID to edit, if any.
        /// </summary>
        public readonly string CommunityId;

        /// <summary>
        /// The cache where the community card view will find the already downloaded textures. If null, the view will use its own.
        /// </summary>
        public readonly ISpriteCache ThumbnailSpriteCache;

        public CommunityCreationEditionParameter(bool canCreateCommunities, string communityId, ISpriteCache thumbnailSpriteCache)
        {
            CanCreateCommunities = canCreateCommunities;
            CommunityId = communityId;
            ThumbnailSpriteCache = thumbnailSpriteCache;
        }
    }
}
