namespace DCL.UI.SuggestionPanel
{
    public interface ISuggestionElementData
    {
        string GetId();

        InputSuggestionType GetInputSuggestionType();
    }

    public struct SuggestionElementData : ISuggestionElementData
    {
        public string GetId() =>
            "ID";

        public InputSuggestionType GetInputSuggestionType() =>
            InputSuggestionType.NONE;
    }
}
