namespace DCL.Communities.CommunityCreation
{
    public struct CommunityCreationEditionParameter
    {
        public readonly bool HasClaimedName;
        public readonly string CommunityId;

        public CommunityCreationEditionParameter(bool hasClaimedName, string communityId)
        {
            HasClaimedName = hasClaimedName;
            CommunityId = communityId;
        }
    }
}
