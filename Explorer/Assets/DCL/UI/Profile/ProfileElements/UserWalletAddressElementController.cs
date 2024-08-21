using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Threading;

namespace DCL.UI.ProfileElements
{
    public class UserWalletAddressElementController : IDisposable
    {
        public readonly UserWalletAddressElement Element;

        private Profile currentProfile;

        public UserWalletAddressElementController(UserWalletAddressElement element)
        {
            this.Element = element;

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
            Element.UserWalletAddressText.text = $"{profile.UserId[..5]}...{profile.UserId[^5..]}";
        }

        public void Dispose()
        {
            Element.CopyWalletAddressButton.onClick.RemoveAllListeners();
            Element.CopyWalletWarningNotification.Hide(true);
        }
    }
}
