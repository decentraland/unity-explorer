using DCL.CharacterPreview;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport
{
    public class PassportView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Button BackgroundButton { get; private set; }

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; }
    }
}
