using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

namespace DCL.UI.SuggestionPanel
{
    public readonly struct ProfileInputSuggestionData : IInputSuggestionElementData
    {
        public Profile ProfileData { get; }
        public ProfileRepositoryWrapper ProfileDataProvider { get; }

        public ProfileInputSuggestionData(Profile profileData, ProfileRepositoryWrapper profileDataProvider)
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
