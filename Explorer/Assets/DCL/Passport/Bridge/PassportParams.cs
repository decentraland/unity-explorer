namespace DCL.Passport
{
    public struct PassportParams
    {
        public string UserId { get; }
        public string? BadgeIdSelected { get; }
        public bool IsOwnProfile { get; }

        public PassportParams(string userId, string? badgeIdSelected = null, bool isOwnProfile = false)
        {
            UserId = userId;
            BadgeIdSelected = badgeIdSelected;
            IsOwnProfile = isOwnProfile;
        }
    }
}
