using DCL.Communities.CommunitiesDataProvider.DTOs;
using System;
using System.Collections.Generic;

namespace DCL.Communities.CommunitiesCard.Members
{
    public static class MembersSorter
    {
        private static readonly Comparison<ICommunityMemberData> CACHED_MEMBER_COMPARISON = MemberDataComparison;

        internal static void SortMembersList(List<ICommunityMemberData> friends) =>
            friends.Sort(CACHED_MEMBER_COMPARISON);

        private static int MemberDataComparison(ICommunityMemberData f1, ICommunityMemberData f2)
        {
            int roleComparison = f2.Role.CompareTo(f1.Role);
            return roleComparison != 0 ? roleComparison : string.Compare(f1.Name, f2.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
