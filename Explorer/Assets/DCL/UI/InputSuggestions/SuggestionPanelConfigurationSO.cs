using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.SuggestionPanel
{
    [CreateAssetMenu(fileName = "SuggestionPanelConfiguration", menuName = "DCL/UI/Suggestion Panel Configuration")]
    public class SuggestionPanelConfigurationSO : ScriptableObject
    {
        [Header("Panel Settings")]
        [field: SerializeField] internal float minHeight = 50;
        [field: SerializeField] internal float entryHeight = 34;
        [field: SerializeField] internal float padding = 16;
        [field: SerializeField] internal float maxHeight = 340;

        //TODO Fran: see how to use this value correctly, right now it needs to be a const so its not used
        [field: SerializeField] internal float maxSuggestions = 7;

        [Header("Suggestion Elements")]
        [field: SerializeField] private List<BaseInputSuggestionElement> suggestionElements;

        public IReadOnlyList<BaseInputSuggestionElement> SuggestionElements => suggestionElements;
    }
}
