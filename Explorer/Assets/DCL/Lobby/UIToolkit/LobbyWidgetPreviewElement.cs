using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Stand-in of a widget while it is dragged off its row of the widget modal: its title over as many blank cards as
    ///     the widget shows, at the size of its span and laid out like a section, with no data behind it.
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

            int cardCount = widgetId.CardCount();

            for (var i = 0; i < cardCount; i++)
            {
                var card = new VisualElement { pickingMode = PickingMode.Ignore };
                card.AddToClassList(USS_CARD);
                cards.Add(card);
            }
        }
    }
}