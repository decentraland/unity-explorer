using DCL.UI.ProfileElements;
using UnityEngine;

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
            profilePictureView.Setup(data.ProfileDataProvider, data.ProfileData);
        }
    }
}
