using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Utility;

namespace DCL.UI.SuggestionPanel
{
    public delegate void SuggestionSelectedDelegate(string suggestionId);

    public class InputSuggestionPanelController
    {
        public event SuggestionSelectedDelegate SuggestionSelectedEvent;

        private readonly InputSuggestionPanelElement suggestionPanel;

        private CancellationTokenSource searchCts = new ();
        private string lastMatch;
        private readonly List<IInputSuggestionElementData> keys = new ();

        public InputSuggestionPanelController(InputSuggestionPanelElement suggestionPanel, ViewDependencies viewDependencies)
        {
            this.suggestionPanel = suggestionPanel;
            this.suggestionPanel.InjectDependencies(viewDependencies);
            this.suggestionPanel.SuggestionSelectedEvent += OnSuggestionSelected;
        }

        private void OnSuggestionSelected(string suggestionId)
        {
            SuggestionSelectedEvent?.Invoke(suggestionId);
        }

        public void SetPanelVisibility(bool isVisible)
        {
            suggestionPanel.SetPanelVisibility(isVisible);
        }

        public Match HandleSuggestionsSearch(string inputText, Regex regex, InputSuggestionType suggestionType, Dictionary<string, IInputSuggestionElementData> suggestionDataMap)
        {
            Match match = regex.Match(inputText);

            if (match.Success && match.Groups.Count > 1)
            {
                searchCts = searchCts.SafeRestart();
                SearchAndSetSuggestionsAsync(match.Groups[1].Value, suggestionType, suggestionDataMap, searchCts.Token).Forget();
                return match;
            }

            if (suggestionPanel.IsActive)
                SetPanelVisibility(false);

            return Match.Empty;
        }

        private async UniTaskVoid SearchAndSetSuggestionsAsync(string value, InputSuggestionType suggestionType, Dictionary<string, IInputSuggestionElementData> suggestionDataMap, CancellationToken ct)
        {
            await DictionaryUtils.GetKeysWithPrefixAsync(suggestionDataMap, value, keys, ct);

            var suggestions = new List<IInputSuggestionElementData>();

            foreach (IInputSuggestionElementData data in keys)
                suggestions.Add(data);

            suggestionPanel.SetSuggestionValues(suggestionType, suggestions);
            suggestionPanel.SetPanelVisibility(true);
        }

        public void Dispose()
        {
            searchCts.SafeCancelAndDispose();
        }
    }
}
