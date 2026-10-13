using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Stand-in of a widget while it is off the grids: its title over as many blank cards as the widget shows, at the
    ///     size of its span and with no data behind them. It can be drawn scaled down from its top-left corner to fit a
    ///     row of the widget modal, and at full size while dragged off it.
    /// </summary>
    public class LobbyWidgetPreviewElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-widget-preview";
        private const string USS_TITLE = USS_BLOCK + "__title";
        private const string USS_CARDS = USS_BLOCK + "__cards";
        private const string USS_CARD = USS_BLOCK + "__card";

        public LobbyWidgetId WidgetId { get; }

        public LobbyWidgetPreviewElement(LobbyWidgetId widgetId, string title)
        {
            WidgetId = widgetId;

            AddToClassList(USS_BLOCK);
            pickingMode = PickingMode.Ignore;
            Vector2 size = LobbyGridElement.SpanToSize(widgetId.Span());
            style.width = size.x;
            style.height = size.y;

            var titleLabel = new Label(title) { name = "Title", pickingMode = PickingMode.Ignore };
            titleLabel.AddToClassList(USS_TITLE);
            Add(titleLabel);

            var cards = new VisualElement { name = "Cards", pickingMode = PickingMode.Ignore };
            cards.AddToClassList(USS_CARDS);
            Add(cards);

            int cardCount = CardCount(widgetId);

            for (var i = 0; i < cardCount; i++)
            {
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                card.AddToClassList(USS_CARD);
                cards.Add(card);
            }
        }

        /// <summary>Draws the preview at a fraction of the size of its span; its layout box keeps the full size.</summary>
        public void SetScale(float scale) =>
            style.scale = new Scale(new Vector2(scale, scale));

        /// <summary>How many cards the widget shows at once.</summary>
        private static int CardCount(LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.Landing4x3 => 1,
                LobbyWidgetId.LiveEvents3x3 => 1,
                LobbyWidgetId.UpcomingEvents3x2 => 1,
                LobbyWidgetId.FriendsOnline4x2 => 4,
                LobbyWidgetId.JumpBackIn6x2 => 3,
                LobbyWidgetId.JumpBackIn4x2 => 2,
                LobbyWidgetId.RecommendedPlaces6x2 => 3,
                LobbyWidgetId.RecommendedPlaces4x2 => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, null),
            };
    }
}
