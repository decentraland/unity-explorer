using DCL.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.Sections.Friends
{
    public class FriendListUserView : FriendPanelUserView
    {
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public Button JumpInButton { get; private set; }

        public override void Configure(Profile profile)
        {
            buttons = new[] { JumpInButton, ContextMenuButton };
            base.Configure(profile);
        }
    }
}
