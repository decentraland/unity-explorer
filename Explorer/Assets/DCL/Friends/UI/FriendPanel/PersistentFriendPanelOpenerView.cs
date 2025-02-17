using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.FriendPanel
{
    public class PersistentFriendPanelOpenerView : ViewBase, IView
    {
        [field: SerializeField]
        public Button OpenFriendPanelButton { get; private set; }

        [field: SerializeField]
        public GameObject FriendsEnabledContainer { get; private set; }

        [field: SerializeField]
        public GameObject FriendsDisabledContainer { get; private set; }

        public void SetButtonStatePanelShow(bool panelShown)
        {
            FriendsDisabledContainer.SetActive(!panelShown);
            FriendsEnabledContainer.SetActive(panelShown);
        }
    }
}
