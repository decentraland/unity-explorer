using DCL.Profiles;

namespace DCL.UI.SuggestionPanel
{
    public readonly struct ProfileInputSuggestionData : IInputSuggestionElementData
    {
        public Profile ProfileData { get; }

        public ProfileInputSuggestionData(Profile profileData)
        {
            ProfileData = profileData;
        }

        public string GetId() =>
            ProfileData.UserId;

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.PROFILE;
    }
}
