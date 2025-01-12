using MVC;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class PastePopupToastView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] internal Button PasteButton { get; private set;}
        [field: SerializeField] internal RectTransform PasteToastPosition { get; private set;}
        [field: SerializeField] internal CanvasGroup CanvasGroup { get; private set;}
    }
}
