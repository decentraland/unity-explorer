using DCL.Profiles;
using System;
using System.Threading;

namespace DCL.UI.ProfileElements
{
    public class UserNameElementController : IDisposable
    {
        private Profile? currentProfile;

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

                UserInfoHelper.CopyToClipboard(currentProfile.HasClaimedName ? element.UserNameText.text : $"{currentProfile.Name}#{currentProfile.UserId[^4..]}");
                UserInfoHelper.ShowCopyWarningAsync(element.CopyNameWarningNotification, CancellationToken.None).Forget();
            });
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            Element.UserNameText.text = profile.ValidatedName;
            Element.UserNameText.color = profile.UserNameColor;
            Element.UserNameHashtagText.text = profile.WalletId;
            Element.UserNameHashtagText.gameObject.SetActive(!profile.HasClaimedName);
            Element.VerifiedMark.SetActive(profile.HasClaimedName);
        }

        public void Dispose()
        {
            Element.CopyUserNameButton.onClick.RemoveAllListeners();
        }
    }
}
