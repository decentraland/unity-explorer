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
        private readonly UserNameElement element;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;

        private Profile currentProfile;

        public UserNameElementController(
            UserNameElement element,
            ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.element = element;
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

            element.UserNameText.text = profile.Name;
            element.UserNameText.color = chatEntryConfiguration.GetNameColor(profile.Name);
            element.UserNameHashtagText.text = $"#{profile.UserId[^4..]}";
            element.UserNameHashtagText.gameObject.SetActive(!profile.HasClaimedName);
            element.VerifiedMark.SetActive(profile.HasClaimedName);
            WaitUntilToNextFrameAsync().Forget();
        }

        private async UniTaskVoid WaitUntilToNextFrameAsync()
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);
            element.LayoutGroup.spacing = 0.01f;
        }

        private void Clear() { }

        public void Dispose()
        {
            element.CopyUserNameButton.onClick.RemoveAllListeners();
            element.CopyNameWarningNotification.Hide(true);
            Clear();
        }

    }
}
