namespace DCL.ApplicationsGuards
{
    public struct BlockedScreenParameters
    {
        public readonly BannedUserData? BannedUserData;

        public BlockedScreenParameters(BannedUserData? bannedUserData)
        {
            BannedUserData = bannedUserData;
        }
    }
}
