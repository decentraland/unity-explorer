
namespace DCL.Communities
{
    public class GetOnlineCommunityMembersResponse
    {
        public class MemberData
        {
            public string id;
            public CommunityMemberRole role;
        }

        public MemberData[] members;
    }
}


