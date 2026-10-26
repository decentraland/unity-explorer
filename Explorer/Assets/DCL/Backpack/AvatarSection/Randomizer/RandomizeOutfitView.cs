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
