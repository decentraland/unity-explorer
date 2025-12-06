using TMPro;
using UnityEngine;

namespace DCL.SocialEmotes.UI
{
    public class SocialEmoteOutcomeChoiceView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text numberText;

        [SerializeField]
        private TMP_Text titleText;

        private static readonly string[] NUMBER_STRINGS = new []{ "1", "2", "3" };

        public void SetTitle(int outcomeIndex, string title)
        {
            numberText.text = NUMBER_STRINGS[outcomeIndex];
            titleText.text = title;
        }
    }
}
