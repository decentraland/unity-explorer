namespace DCL.Passport
{
    public partial class PassportController
    {
        public struct Params
        {
            public string UserId { get; }
            public string? BadgeIdSelected { get; }
            public bool IsOwnProfile { get; }

            public Params(string userId, string? badgeIdSelected = null, bool isOwnProfile = false)
            {
                UserId = userId;
                BadgeIdSelected = badgeIdSelected;
                IsOwnProfile = isOwnProfile;
            }
        }
    }
}
