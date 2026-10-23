using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     A lobby row of the user's communities, one <see cref="LobbyCommunityCardElement" /> per community, with a
    ///     banner while there is none; it only reports which card was clicked.
    /// </summary>
    public class LobbyCommunitiesRail : LobbyCardRail<LobbyCommunityCardElement>
    {
        public Action<int>? CardClicked;

        public LobbyCommunitiesRail(VisualTreeAsset cardTemplate) : base(cardTemplate, showsEmptyState: true) { }

        protected override void OnCardCreated(LobbyCommunityCardElement card, int index)
        {
            card.Clicked = () => CardClicked?.Invoke(index);
        }
    }
}
