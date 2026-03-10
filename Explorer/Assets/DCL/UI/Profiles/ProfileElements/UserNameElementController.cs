using DCL.FeatureFlags;
using DCL.Profiles;
using System;
using System.Threading;

namespace DCL.UI.ProfileElements
{
    public class UserNameElementController : IDisposable
    {
        private Profile.CompactInfo? currentProfile;

        public readonly UserNameElement Element;

        public UserNameElementController(
            UserNameElement element)
        {
            Element = element;

            element.CopyNameWarningNotification.Hide(true);

            element.CopyUserNameButton.onClick.AddListener(() =>
            {
                if (currentProfile == null)
                    return;

                UserInfoHelper.CopyToClipboard(currentProfile.Value.HasClaimedName ? element.UserNameText.text : $"{currentProfile.Value.Name}{currentProfile.Value.WalletId}");
                UserInfoHelper.ShowCopyWarningAsync(element.CopyNameWarningNotification, CancellationToken.None).Forget();
            });
        }

        public void Setup(Profile.CompactInfo profile)
        {
            currentProfile = profile;

            Element.UserNameText.text = profile.ValidatedName;
            Element.UserNameText.color = profile.UserNameColor;
            Element.UserNameHashtagText.text = profile.WalletId;
            Element.UserNameHashtagText.gameObject.SetActive(!profile.HasClaimedName);
            Element.VerifiedMark.SetActive(profile.HasClaimedName);
            Element.OfficialMark.SetActive(OfficialWalletsHelper.Instance.IsOfficialWallet(profile.UserId));
        }

        public void Dispose()
        {
            Element.CopyUserNameButton.onClick.RemoveAllListeners();
        }
    }
}
