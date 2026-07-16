namespace DCL.ApplicationGuards
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
