namespace DCL.UI.ProfileElements
{
    public readonly struct ProfileOptionalBasicInfo
    {
        public readonly string UserName;
        public readonly string? UserWalletId;
        public readonly bool DataIsPresent;

        public ProfileOptionalBasicInfo(bool dataIsPresent, string userName, string? userWalletId)
        {
            DataIsPresent = dataIsPresent;
            UserName = userName;
            UserWalletId = userWalletId;
        }

        public static ProfileOptionalBasicInfo Default() =>
            new (false, string.Empty, null);
    }
}
