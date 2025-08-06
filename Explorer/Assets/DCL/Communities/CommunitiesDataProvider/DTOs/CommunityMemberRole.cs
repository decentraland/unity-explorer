using System;

// ReSharper disable InconsistentNaming
namespace DCL.Communities
{
    [Serializable]
    public enum CommunityMemberRole
    {
        member,
        moderator,
        owner,
        none,
        unknown,
    }

    public static class CommunityMemberRoleExtensions
    {
        public static bool IsAnyMod(this CommunityMemberRole role) =>
            role is CommunityMemberRole.member or CommunityMemberRole.moderator;

        public static bool IsAnyMember(this CommunityMemberRole role) =>
            role is CommunityMemberRole.member or CommunityMemberRole.moderator or CommunityMemberRole.owner;
    }
}



