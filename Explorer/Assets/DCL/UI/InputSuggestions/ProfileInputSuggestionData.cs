using DCL.Profiles;
using UnityEngine;

namespace DCL.UI.SuggestionPanel
{
    public readonly struct ProfileInputSuggestionData : IInputSuggestionElementData
    {
        public Profile ProfileData { get; }
        public Color UsernameColor { get; }

        public ProfileInputSuggestionData(Profile profileData, Color usernameColor)
        {
            ProfileData = profileData;
            UsernameColor = usernameColor;
        }

        //TODO FRAN: This is probably not right, we might need to extract this data into this struct directly
        public string GetId() =>
            ProfileData.UserId + "@" + ProfileData.DisplayName;

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.PROFILE;
    }
}
