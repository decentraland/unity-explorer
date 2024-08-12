using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Profiles;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements
{
    public class UserNameElementController
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
            WaitUntilToNextFrameAsync().Forget();
        }

        private async UniTaskVoid WaitUntilToNextFrameAsync()
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);
            Element.LayoutGroup.spacing = 0.01f;
        }

        public void Dispose()
        {
            Element.CopyUserNameButton.onClick.RemoveAllListeners();
            Element.CopyNameWarningNotification.Hide(true);
        }

    }
}
