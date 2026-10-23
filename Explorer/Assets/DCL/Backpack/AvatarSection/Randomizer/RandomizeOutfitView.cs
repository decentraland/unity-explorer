using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.AvatarSection.Randomizer
{
    public class RandomizeOutfitView : MonoBehaviour
    {
        [field: SerializeField]
        public Button UndoButton { get; private set; } = null!;

        [field: SerializeField]
        public Button RandomizeButton { get; private set; } = null!;

        [field: SerializeField]
        public Button RedoButton { get; private set; } = null!;

        [field: SerializeField]
        public CanvasGroup UndoCanvasGroup { get; private set; } = null!;

        [field: SerializeField]
        public CanvasGroup RedoCanvasGroup { get; private set; } = null!;
    }
}
