using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System.Threading;

namespace DCL.UI.ProfileElements
{
    public class UserWalletAddressElementController
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
            WaitUntilToNextFrameAsync().Forget();
        }


        private async UniTaskVoid WaitUntilToNextFrameAsync()
        {
            Element.LayoutGroup.spacing =- 0.00001f;
            await UniTask.NextFrame(PlayerLoopTiming.LastUpdate);
            Element.LayoutGroup.spacing =+ 0.00001f;
        }

        public void Dispose()
        {
            Element.CopyWalletAddressButton.onClick.RemoveAllListeners();
            Element.CopyWalletWarningNotification.Hide(true);
        }
    }
}
