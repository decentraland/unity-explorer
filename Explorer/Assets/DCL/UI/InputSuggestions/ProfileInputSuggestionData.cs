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

        public string GetId() =>
            ProfileData.UserId;

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.PROFILE;
    }
}
