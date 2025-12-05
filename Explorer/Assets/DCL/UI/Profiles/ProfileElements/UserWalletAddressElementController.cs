using DCL.Profiles;
using System;
using System.Threading;

namespace DCL.UI.ProfileElements
{
    public class UserWalletAddressElementController : IDisposable
    {
        public readonly UserWalletAddressElement Element;

        private string currentProfileId;

        public UserWalletAddressElementController(UserWalletAddressElement element)
        {
            this.Element = element;

            element.CopyWalletWarningNotification.Hide(true);
            element.CopyWalletAddressButton.onClick.AddListener(() =>
            {
                if (currentProfileId == null)
                    return;

                UserInfoHelper.CopyToClipboard(currentProfileId);
                UserInfoHelper.ShowCopyWarningAsync(element.CopyWalletWarningNotification, CancellationToken.None).Forget();
            });
        }

        public void Setup(Profile profile)
        {
            currentProfileId = profile.UserId;
            Element.UserWalletAddressText.text = $"{profile.UserId[..5]}...{profile.UserId[^5..]}";
        }

        public void Setup(string profileId)
        {
            currentProfileId = profileId;
            Element.UserWalletAddressText.text = $"{profileId[..5]}...{profileId[^5..]}";
        }

        public void Dispose()
        {
            Element.CopyWalletAddressButton.onClick.RemoveAllListeners();
            Element.CopyWalletWarningNotification.Hide(true);
        }
    }
}
