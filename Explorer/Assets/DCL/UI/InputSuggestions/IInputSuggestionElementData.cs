namespace DCL.UI.SuggestionPanel
{
    public interface IInputSuggestionElementData
    {
        string GetId();

        InputSuggestionType GetInputSuggestionType();
    }

    public struct InputSuggestionElementData : IInputSuggestionElementData
    {
        public string GetId() =>
            "ID";

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.NONE;
    }
}
