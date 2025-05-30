using System;
using System.Collections.Generic;

namespace DCL.Communities.CommunitiesCard.Members
{
    public static class MembersSorter
    {
        private static readonly Comparison<GetCommunityMembersResponse.MemberData> CACHED_MEMBER_COMPARISON = MemberDataComparison;

        internal static void SortMembersList(List<GetCommunityMembersResponse.MemberData> friends) =>
            friends.Sort(CACHED_MEMBER_COMPARISON);

        private static int MemberDataComparison(GetCommunityMembersResponse.MemberData f1, GetCommunityMembersResponse.MemberData f2)
        {
            int roleComparison = f2.role.CompareTo(f1.role);
            return roleComparison != 0 ? roleComparison : string.Compare(f1.name, f2.name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
