using System;

namespace DCL.Emoji
{
    public class EmojiPanelController
    {
        public event Action<string> OnEmojiSelected;
        private readonly EmojiPanelView view;

        public EmojiPanelController(EmojiPanelView view)
        {
            this.view = view;
            view.OnEmojiSelected += emoji => OnEmojiSelected?.Invoke(emoji);
        }
    }
}
