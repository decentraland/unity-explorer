using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

namespace DCL.UI.SuggestionPanel
{
    public readonly struct ProfileInputSuggestionData : IInputSuggestionElementData
    {
        public Profile.CompactInfo ProfileData { get; }
        public ProfileRepositoryWrapper ProfileDataProvider { get; }

        public ProfileInputSuggestionData(Profile.CompactInfo profileData, ProfileRepositoryWrapper profileDataProvider)
        {
            ProfileData = profileData;
            ProfileDataProvider = profileDataProvider;
        }

        public string GetId() =>
            ProfileData.UserId;

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.PROFILE;
    }
}
