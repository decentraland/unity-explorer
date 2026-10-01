using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The online friends row of the lobby, one <see cref="LobbyFriendCardElement" /> per friend, with the online count of the
    ///     section header. It only reports which card, or which card's Join, was clicked.
    /// </summary>
    public class LobbyFriendsRail : LobbyCardRail<LobbyFriendCardElement>
    {
        private const string ONLINE_COUNT_SUFFIX = " Online";
        private const string ONLINE_COUNT_NAME = "OnlineCount";

        private int shownOnlineCount = -1;

        public Action<int>? CardClicked;
        public Action<int>? CardJoinClicked;

        public LobbyFriendsRail(VisualTreeAsset cardTemplate) : base(cardTemplate) { }

        protected override void OnCardCreated(LobbyFriendCardElement card, int index)
        {
            card.Clicked = () => CardClicked?.Invoke(index);
            card.JoinClicked = () => CardJoinClicked?.Invoke(index);
        }

        protected override void OnCountChanged(int count)
        {
            if (count == shownOnlineCount) return;

            Section!.Q<Label>(ONLINE_COUNT_NAME).text = string.Concat(count.ToString(), ONLINE_COUNT_SUFFIX);
            shownOnlineCount = count;
        }
    }
}
