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
            simpleUserNameElement.Setup(data.ProfileData, data.UsernameColor);
            faceFrame.color = data.UsernameColor;
            //Disabled for now, we need to implement the profile picture fetching.
            //The data should probably be stored in the profile so we don't need to have access to webRequestController everywhere.
            //Potentially add a ProfileDisplayData to the profile and store both the color and the profile picture
            //faceSnapshotImage.SetImage(data.UserFaceIcon);
        }
    }
}
