using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.AvatarSection.Randomizer
{
    public class RandomizeOutfitView : MonoBehaviour
    {
        private const float LOCKED_ALPHA = 0.2f;

        public Button.ButtonClickedEvent UndoClicked => UndoButton.onClick;

        public Button.ButtonClickedEvent RandomizeClicked => RandomizeButton.onClick;

        public Button.ButtonClickedEvent RedoClicked => RedoButton.onClick;

        [field: SerializeField]
        public Button UndoButton { get; internal set; } = null!;

        [field: SerializeField]
        public Button RandomizeButton { get; internal set; } = null!;

        [field: SerializeField]
        public Button RedoButton { get; internal set; } = null!;

        [field: SerializeField]
        public CanvasGroup UndoCanvasGroup { get; internal set; } = null!;

        [field: SerializeField]
        public CanvasGroup RedoCanvasGroup { get; internal set; } = null!;

        [field: SerializeField]
        public CanvasGroup RandomizeIconCanvasGroup { get; internal set; } = null!;

        [field: SerializeField]
        public CanvasGroup RandomizeLabelCanvasGroup { get; internal set; } = null!;

        /// <summary>
        ///     A locked button fades its content and keeps its container, so the bar keeps its shape.
        /// </summary>
        public void SetState(bool canRandomize, bool canUndo, bool canRedo)
        {
            SetLocked(RandomizeButton, RandomizeIconCanvasGroup, !canRandomize);
            RandomizeLabelCanvasGroup.alpha = RandomizeIconCanvasGroup.alpha;
            SetLocked(UndoButton, UndoCanvasGroup, !canUndo);
            SetLocked(RedoButton, RedoCanvasGroup, !canRedo);
        }

        private static void SetLocked(Button button, CanvasGroup content, bool locked)
        {
            button.interactable = !locked;
            content.alpha = locked ? LOCKED_ALPHA : 1f;
        }
    }
}
