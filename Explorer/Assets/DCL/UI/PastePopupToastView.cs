using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class PastePopupToastView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField]
        public Button PasteButton { get; private set;}

        [field: SerializeField]
        public RectTransform PasteToastPosition { get; private set;}

    }
}
