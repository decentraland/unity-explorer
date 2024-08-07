using DCL.Profiles;
using System.Threading;

namespace DCL.Passport.Modules
{
    public class UserWalletAddressElementController
    {
        private readonly UserWalletAddressElement element;

        private Profile currentProfile;

        public UserWalletAddressElementController(UserWalletAddressElement element)
        {
            this.element = element;

            element.CopyWalletWarningNotification.Hide(true);
            element.CopyWalletAddressButton.onClick.AddListener(() =>
            {
                if (currentProfile == null)
                    return;

                UserInfoHelper.CopyToClipboard(currentProfile.UserId);
                UserInfoHelper.ShowCopyWarningAsync(element.CopyWalletWarningNotification, CancellationToken.None).Forget();
            });
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;
            element.UserWalletAddressText.text = $"{profile.UserId[..5]}...{profile.UserId[^5..]}";
        }

        public void Clear() { }

        public void Dispose()
        {
            element.CopyWalletAddressButton.onClick.RemoveAllListeners();
            element.CopyWalletWarningNotification.Hide(true);
            Clear();
        }
    }
}
