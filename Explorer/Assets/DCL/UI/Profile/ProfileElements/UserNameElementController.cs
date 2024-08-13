using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Profiles;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements
{
    public class UserNameElementController : IDisposable
    {
        public readonly UserNameElement Element;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;

        private Profile currentProfile;

        public UserNameElementController(
            UserNameElement element,
            ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.Element = element;
            this.chatEntryConfiguration = chatEntryConfiguration;

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

            Element.UserNameText.text = profile.Name;
            Element.UserNameText.color = chatEntryConfiguration.GetNameColor(profile.Name);
            Element.UserNameHashtagText.text = $"#{profile.UserId[^4..]}";
            Element.UserNameHashtagText.gameObject.SetActive(!profile.HasClaimedName);
            Element.VerifiedMark.SetActive(profile.HasClaimedName);
            //Element.LayoutGroup.SetLayoutHorizontal();
        }


        public void Dispose()
        {
            Element.CopyUserNameButton.onClick.RemoveAllListeners();
            Element.CopyNameWarningNotification.Hide(true);
        }

    }
}
