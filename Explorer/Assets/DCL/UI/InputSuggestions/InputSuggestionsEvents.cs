namespace DCL.UI.SuggestionPanel
{
    public static class InputSuggestionsEvents
    {
        public readonly struct SuggestionSelectedEvent
        {
            public readonly string Id;

            public SuggestionSelectedEvent(string id)
            {
                Id = id;
            }
        }
    }
}
