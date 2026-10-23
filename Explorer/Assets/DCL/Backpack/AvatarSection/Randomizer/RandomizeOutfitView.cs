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
        internal Button UndoButton { get; set; } = null!;

        [field: SerializeField]
        internal Button RandomizeButton { get; set; } = null!;

        [field: SerializeField]
        internal Button RedoButton { get; set; } = null!;

        [field: SerializeField]
        internal CanvasGroup UndoCanvasGroup { get; set; } = null!;

        [field: SerializeField]
        internal CanvasGroup RedoCanvasGroup { get; set; } = null!;

        /// <summary>
        ///     Locked undo and redo fade the whole button instead of greying it, so the arrows stay readable.
        /// </summary>
        public void SetState(bool canRandomize, bool canUndo, bool canRedo)
        {
            RandomizeButton.interactable = canRandomize;
            SetLocked(UndoButton, UndoCanvasGroup, !canUndo);
            SetLocked(RedoButton, RedoCanvasGroup, !canRedo);
        }

        private static void SetLocked(Button button, CanvasGroup canvasGroup, bool locked)
        {
            button.interactable = !locked;
            canvasGroup.alpha = locked ? LOCKED_ALPHA : 1f;
        }
    }
}
