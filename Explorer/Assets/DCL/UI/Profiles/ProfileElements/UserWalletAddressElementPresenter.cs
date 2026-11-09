using DCL.Profiles;
using DCL.Web3;
using System;
using System.Threading;

namespace DCL.UI.ProfileElements
{
    public class UserWalletAddressElementPresenter : IDisposable
    {
        public readonly UserWalletAddressElement Element;

        private string? currentProfileId;

        public UserWalletAddressElementPresenter(UserWalletAddressElement element)
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

        public void Setup(Profile.CompactInfo profile)
        {
            Setup(profile.UserId.Value);
        }

        public void Setup(string profileId)
        {
            currentProfileId = profileId;
            Element.UserWalletAddressText.text = Web3Address.Shorten(profileId);
        }

        public void Dispose()
        {
            Element.CopyWalletAddressButton.onClick.RemoveAllListeners();
            Element.CopyWalletWarningNotification.Hide(true);
        }
    }
}
