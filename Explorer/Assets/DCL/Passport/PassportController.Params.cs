namespace DCL.Passport
{
    public partial class PassportController
    {
        public struct Params
        {
            public string UserId { get; }
            public string? BadgeIdSelected { get; }

            public Params(string userId, string? badgeIdSelected = null)
            {
                UserId = userId;
                BadgeIdSelected = badgeIdSelected;
            }
        }
    }
}
