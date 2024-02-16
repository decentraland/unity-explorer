using DCL.CharacterPreview;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.CharacterPreview
{
    public class BackpackCharacterPreviewView : ViewBase, IView
    {
        [field: SerializeField] public RawImage RawImage { get; private set; }
        [field: SerializeField] public CharacterPreviewInputDetector CharacterPreviewInputDetector { get; private set; }
        [field: SerializeField] public BackpackCharacterPreviewCursorView CharacterPreviewCursorView { get; private set; }
    }
}
