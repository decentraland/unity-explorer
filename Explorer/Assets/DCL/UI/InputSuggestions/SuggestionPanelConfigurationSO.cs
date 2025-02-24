using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.SuggestionPanel
{
    [CreateAssetMenu(fileName = "SuggestionPanelConfiguration", menuName = "DCL/UI/Suggestion Panel Configuration")]
    public class SuggestionPanelConfigurationSO : ScriptableObject
    {
        [Header("Panel Settings")]
        [field: SerializeField] internal float minHeight = 50;
        [field: SerializeField] internal float padding = 16;
        [field: SerializeField] internal float maxHeight = 340;

        [Header("Suggestion Elements")]
        [field: SerializeField] private List<BaseInputSuggestionElement> suggestionElements;

        public IReadOnlyList<BaseInputSuggestionElement> SuggestionElements => suggestionElements;
    }
}
