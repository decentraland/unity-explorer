using System;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     A lobby row of the user's camera reel photos, one <see cref="LobbyGalleryCardElement" /> per photo, with a
    ///     banner while there is none; it only reports which card was clicked.
    /// </summary>
    public class LobbyGalleryRail : LobbyCardRail<LobbyGalleryCardElement>
    {
        public Action<int>? CardClicked;

        public LobbyGalleryRail(VisualTreeAsset cardTemplate) : base(cardTemplate, showsEmptyState: true) { }

        protected override void OnCardCreated(LobbyGalleryCardElement card, int index)
        {
            card.Clicked = () => CardClicked?.Invoke(index);
        }
    }
}
