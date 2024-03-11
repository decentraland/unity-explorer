using System.Collections.Generic;

namespace DCL.Emoji
{
    public class EmojiSuggestionPanel
    {
        private readonly EmojiSuggestionPanelView view;

        public EmojiSuggestionPanel(EmojiSuggestionPanelView view)
        {
            this.view = view;
        }

        public void SetValues(List<EmojiData> foundEmojis)
        {
            foreach (EmojiData foundEmoji in foundEmojis)
            {

            }
        }

        public void SetActive(bool isActive)
        {
            view.gameObject.SetActive(isActive);
        }
    }
}
