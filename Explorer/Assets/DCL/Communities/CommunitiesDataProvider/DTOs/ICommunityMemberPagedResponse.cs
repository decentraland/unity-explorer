using System.Collections.Generic;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    public interface ICommunityMemberPagedResponse
    {
        IReadOnlyList<ICommunityMemberData> members { get; }
        int total { get; }
    }
}
