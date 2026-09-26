using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.CharacterPreview
{
    public class BackpackCharacterPreviewCursorView : ViewBase, IView
    {
        [field: SerializeField] public Image CursorOverrideImage { get; private set; }
        [field: SerializeField] internal Sprite rotateCursor { get; private set; }
        [field: SerializeField] internal Sprite panCursor { get; private set; }
    }
}
