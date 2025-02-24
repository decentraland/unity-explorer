using Cysharp.Threading.Tasks;
using MVC;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Utility;

namespace DCL.UI.SuggestionPanel
{
    public delegate void SuggestionSelectedDelegate(string suggestionId);

    public class InputSuggestionPanelController
    {
        public event SuggestionSelectedDelegate SuggestionSelected;

        private readonly InputSuggestionPanelView suggestionPanel;

        private CancellationTokenSource searchCts = new ();
        private string lastMatch;

        public InputSuggestionPanelController(InputSuggestionPanelView suggestionPanel, ViewDependencies viewDependencies)
        {
            this.suggestionPanel = suggestionPanel;
            this.suggestionPanel.InjectDependencies(viewDependencies);
            this.suggestionPanel.SuggestionSelected += OnSuggestionSelected;
        }

        private void OnSuggestionSelected(string suggestionId)
        {
            SuggestionSelected?.Invoke(suggestionId);
        }

        public void SetPanelVisibility(bool isVisible)
        {
            suggestionPanel.SetPanelVisibility(isVisible);
        }

        /// <summary>
        /// Processes the text using the sent Regex, checking in the suggestionDataMap for coincidences and returns if there was a match.
        /// If there was a match, it displays the suggestionPanel with the list of suggestions obtained from the suggestionDataMap that match.
        /// </summary>
        /// <param name="inputText"> The text where the suggestions must be matched </param>
        /// <param name="regex"> The Regex used to match the suggestions </param>
        /// <param name="suggestionType"> The Type of Suggestion, used when displaying the suggestions </param>
        /// <param name="suggestionDataMap"> The Dictionary that contains all the possible suggestion data values to display</param>
        /// <typeparam name="T"> The type of suggestion element data</typeparam>
        /// <returns> The match if there was one or an empty match if there was none </returns>
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
