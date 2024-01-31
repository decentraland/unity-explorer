using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewView : ViewBase, IView
    {
        [field: SerializeField] public RawImage RawImage { get; private set; }
        [field: SerializeField] public CharacterPreviewInputDetector CharacterPreviewInputDetector { get; private set; }
        [field: SerializeField] public CharacterPreviewCursorContainer CharacterPreviewCursorContainer { get; private set; }
        [field: SerializeField] public CharacterPreviewSettingsSO CharacterPreviewSettingsSo { get; private set;}
    }
}
