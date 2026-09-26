using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class ChatEntryMenuPopupView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] internal Button CopyButton { get; private set;}
        [field: SerializeField] internal RectTransform MenuPosition { get; private set;}
        [field: SerializeField] internal CanvasGroup CanvasGroup { get; private set;}
    }
}
