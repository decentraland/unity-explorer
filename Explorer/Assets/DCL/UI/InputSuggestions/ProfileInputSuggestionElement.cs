using Cysharp.Threading.Tasks;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI.SuggestionPanel
{
    public class ProfileInputSuggestionElement : BaseInputSuggestionElement<ProfileInputSuggestionData>
    {
        [field: SerializeField] private ProfilePictureView profilePictureView;
        [field: SerializeField] private SimpleUserNameElement simpleUserNameElement;

        private readonly ReactiveProperty<ProfileThumbnailViewModel> thumbnail = new (ProfileThumbnailViewModel.Default());
        private CancellationTokenSource? thumbnailCts;

        protected override void SetupContinuation(ProfileInputSuggestionData data)
        {
            SuggestionId = data.ProfileData.DisplayName;
            simpleUserNameElement.Setup(data.ProfileData);

            thumbnail.UpdateValue(ProfileThumbnailViewModel.Default(data.ProfileData.UserNameColor));
            profilePictureView.Bind(thumbnail);
            thumbnailCts = thumbnailCts.SafeRestart();
            GetProfileThumbnailCommand.Instance.ExecuteAsync(thumbnail, null, data.ProfileData, thumbnailCts.Token).Forget();
        }

        private void OnDestroy()
        {
            thumbnailCts.SafeCancelAndDispose();
        }
    }
}
