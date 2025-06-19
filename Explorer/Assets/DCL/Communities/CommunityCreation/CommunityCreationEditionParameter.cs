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

        public CommunityCreationEditionParameter(bool canCreateCommunities, string communityId)
        {
            CanCreateCommunities = canCreateCommunities;
            CommunityId = communityId;
        }
    }
}
