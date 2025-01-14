using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.Sections.Friends
{
    public class FriendListUserView : FriendPanelUserView
    {
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public Button JumpInButton { get; private set; }

        private void Start()
        {
            buttons = new[] { JumpInButton, ContextMenuButton };
        }
    }
}
