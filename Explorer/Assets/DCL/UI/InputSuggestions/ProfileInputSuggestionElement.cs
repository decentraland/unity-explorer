using DCL.UI.ProfileElements;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SuggestionPanel
{
    public class ProfileInputSuggestionElement : BaseInputSuggestionElement<ProfileInputSuggestionData>
    {
        [field: SerializeField] private Image faceSnapshotImage;
        [field: SerializeField] private Image faceFrame;
        [field: SerializeField] private SimpleUserNameElement simpleUserNameElement;

        protected override void SetupContinuation(ProfileInputSuggestionData data)
        {
            SuggestionId = data.ProfileData.DisplayName;
            simpleUserNameElement.Setup(data.ProfileData, data.ProfileData.UserNameColor);
            faceFrame.color = data.ProfileData.UserNameColor;
            //faceSnapshotImage.sprite = data.ProfileData.ProfilePicture
            //TODO FRAN issue #3276-> Disabled for now, we need to implement the profile picture fetching properly and reuse of profile element.
        }
    }
}
