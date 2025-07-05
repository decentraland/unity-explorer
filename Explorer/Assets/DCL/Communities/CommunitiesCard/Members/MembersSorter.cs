using System;
using System.Collections.Generic;
using MemberData = DCL.Communities.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public static class MembersSorter
    {
        private static readonly Comparison<MemberData> CACHED_MEMBER_COMPARISON = MemberDataComparison;

        internal static void SortMembersList(List<MemberData> friends) =>
            friends.Sort(CACHED_MEMBER_COMPARISON);

        private static int MemberDataComparison(MemberData f1, MemberData f2)
        {
            int roleComparison = f2.role.CompareTo(f1.role);
            return roleComparison != 0 ? roleComparison : string.Compare(f1.name, f2.name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
