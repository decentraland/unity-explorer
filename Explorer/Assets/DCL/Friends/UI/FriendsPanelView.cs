using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI
{
    public class FriendsPanelView: ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public Button CloseButton { get; private set; }
    }
}
