namespace DCL.Passport
{
    public partial class PassportController
    {
        public struct Params
        {
            public string UserId { get; }

            public Params(string userId)
            {
                UserId = userId;
            }
        }
    }
}
