using System.Collections.Generic;
using MemberData = DCL.Communities.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class SectionFetchData
    {
        private readonly int pageSize;

        public int pageNumber;
        public int totalFetched;
        public int totalToFetch;

        public readonly List<MemberData> members;

        public SectionFetchData(int pageSize)
        {
            this.pageSize = pageSize;
            pageNumber = 0;
            totalFetched = 0;
            totalToFetch = 0;
            members = new List<MemberData>(pageSize);
        }

        public void Reset()
        {
            pageNumber = 0;
            totalFetched = 0;
            totalToFetch = 0;
            members.Clear();
        }
    }
}
