
using System;

namespace DCL.Communities
{
    [Serializable]
    public class GetOnlineCommunityMembersResponse
    {
        [Serializable]
        public struct MemberData
        {
            public string id;
            public CommunityMemberRole role;
        }

        public MemberData[] members;
    }
}


