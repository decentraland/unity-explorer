using DCL.Profiles;
using MVC;

namespace DCL.UI.SuggestionPanel
{
    public readonly struct ProfileInputSuggestionData : IInputSuggestionElementData
    {
        public Profile ProfileData { get; }
        public ViewDependencies ViewDependencies { get; }

        public ProfileInputSuggestionData(Profile profileData, ViewDependencies viewDependencies)
        {
            ProfileData = profileData;
            ViewDependencies = viewDependencies;
        }

        public string GetId() =>
            ProfileData.UserId;

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.PROFILE;
    }
}
