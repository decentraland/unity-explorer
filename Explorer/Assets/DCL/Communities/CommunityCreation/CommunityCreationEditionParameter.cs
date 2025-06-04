namespace DCL.Communities.CommunityCreation
{
    public struct CommunityCreationEditionParameter
    {
        public readonly bool CanCreateCommunities;
        public readonly string CommunityId;

        public CommunityCreationEditionParameter(bool canCreateCommunities, string communityId)
        {
            CanCreateCommunities = canCreateCommunities;
            CommunityId = communityId;
        }
    }
}
