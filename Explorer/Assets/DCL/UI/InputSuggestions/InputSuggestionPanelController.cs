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
            this.suggestionPanel.Initialize();
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

        public string HandleSuggestionsSearch(string inputText, Regex regex, InputSuggestionType suggestionType, Dictionary<string, IInputSuggestionElementData> suggestionDataMap)
        {
            Match match = regex.Match(inputText);

            if (match.Success)
            {
                searchCts = searchCts.SafeRestart();
                SearchAndSetSuggestionsAsync(match.Value, suggestionType, suggestionDataMap, searchCts.Token).Forget();
                return match.Value;
            }

            if (suggestionPanel.IsActive)
                SetPanelVisibility(false);

            return null;
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
