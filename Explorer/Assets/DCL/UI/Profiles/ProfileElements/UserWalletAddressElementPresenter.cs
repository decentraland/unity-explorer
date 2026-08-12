using DCL.Profiles;
using System;
using System.Threading;

namespace DCL.UI.ProfileElements
{
    public class UserWalletAddressElementPresenter : IDisposable
    {
        public readonly UserWalletAddressElement Element;

        private string currentProfileId;

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
            string userId = profile.UserId.Value;
            currentProfileId = userId;
            Element.UserWalletAddressText.text = $"{userId[..5]}...{userId[^5..]}";
        }

        public void Setup(string profileId)
        {
            currentProfileId = profileId;
            Element.UserWalletAddressText.text = $"{profileId[..5]}...{profileId[^5..]}";
        }

        public void Clear()
        {
            currentProfileId = null;
            Element.UserWalletAddressText.text = string.Empty;
            Element.CopyWalletWarningNotification.Hide(true);
        }

        public void Dispose()
        {
            Element.CopyWalletAddressButton.onClick.RemoveAllListeners();
            Element.CopyWalletWarningNotification.Hide(true);
        }
    }
}
