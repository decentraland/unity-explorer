namespace DCL.Friends.UI.FriendPanel
{
    public struct FriendsPanelParameter
    {
        public readonly FriendsPanelController.FriendsPanelTab TabToShow;

        public FriendsPanelParameter(FriendsPanelController.FriendsPanelTab tabToShow)
        {
            TabToShow = tabToShow;
        }
    }
}
