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

        public Match HandleSuggestionsSearch<T>(string inputText, Regex regex, InputSuggestionType suggestionType, Dictionary<string, T> suggestionDataMap) where T : IInputSuggestionElementData
        {
            Match match = regex.Match(inputText);

            if (match.Success && match.Groups.Count > 1)
            {
                searchCts = searchCts.SafeRestart();
                SearchAndSetSuggestionsAsync<T>(match.Groups[1].Value, suggestionType, suggestionDataMap, searchCts.Token).Forget();
                return match;
            }

            if (suggestionPanel.IsActive)
                SetPanelVisibility(false);

            return Match.Empty;
        }

        private async UniTaskVoid SearchAndSetSuggestionsAsync<T>(string value, InputSuggestionType suggestionType, Dictionary<string, T> suggestionDataMap, CancellationToken ct) where T : IInputSuggestionElementData
        {
            var resultList = new List<T>();
            await DictionaryUtils.GetKeysWithPrefixAsync<T>(suggestionDataMap, value, resultList, ct);


            suggestionPanel.SetSuggestionValues(suggestionType, resultList);
            suggestionPanel.SetPanelVisibility(true);
        }

        public void Dispose()
        {
            searchCts.SafeCancelAndDispose();
        }
    }
}
