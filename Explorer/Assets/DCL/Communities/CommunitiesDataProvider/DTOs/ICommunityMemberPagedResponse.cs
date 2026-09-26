namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    public interface ICommunityMemberPagedResponse
    {
        ICommunityMemberData[] members { get; }
        int total { get; }
    }
}
