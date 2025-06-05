using DCL.UI.ProfileElements;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SuggestionPanel
{
    public class ProfileInputSuggestionElement : BaseInputSuggestionElement<ProfileInputSuggestionData>
    {
        [field: SerializeField] private ProfilePictureView profilePictureView;
        [field: SerializeField] private SimpleUserNameElement simpleUserNameElement;

        protected override void SetupContinuation(ProfileInputSuggestionData data)
        {
            SuggestionId = data.ProfileData.DisplayName;
            simpleUserNameElement.Setup(data.ProfileData);
            profilePictureView.SetupWithDependencies(data.ProfileDataProvider, data.ProfileData.UserNameColor, data.ProfileData.Avatar.FaceSnapshotUrl, data.ProfileData.UserId);
        }
    }
}
