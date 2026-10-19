using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Stand-in of a widget while it is dragged off its row of the widget modal: its title over as many blank entries
    ///     as the widget shows, each the size of the real card, and under them the pager of the widgets that page, all at
    ///     the size of its span and laid out like the section, with no data behind it. The stylesheet knows the metrics
    ///     of each family through the modifier the family puts on the element.
    /// </summary>
    public class LobbyWidgetPreviewElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-widget-preview";
        private const string USS_LANDING = USS_BLOCK + "--landing";
        private const string USS_LIVE_EVENTS = USS_BLOCK + "--live-events";
        private const string USS_UPCOMING_EVENTS = USS_BLOCK + "--upcoming-events";
        private const string USS_FRIENDS = USS_BLOCK + "--friends";
        private const string USS_PLACES = USS_BLOCK + "--places";
        private const string USS_GALLERY = USS_BLOCK + "--gallery";
        private const string USS_GO_TO = USS_BLOCK + "--go-to";
        private const string USS_NARROW = USS_BLOCK + "--narrow";
        private const string USS_TITLE = USS_BLOCK + "__title";
        private const string USS_CARDS = USS_BLOCK + "__cards";
        private const string USS_CARD = USS_BLOCK + "__card";
        private const string USS_PAGER = USS_BLOCK + "__pager";
        private const string USS_PAGER_DOT = USS_BLOCK + "__pager-dot";
        private const string USS_PAGER_DOT_SELECTED = USS_PAGER_DOT + "--selected";

        /// <summary>Dots the pager stands in with; the first is drawn as the selected one.</summary>
        private const int PAGER_DOTS = 3;

        private readonly Label titleLabel;

        public LobbyWidgetId WidgetId { get; }

        /// <summary>What the stand-in is titled; the widget's own title, so the two read alike.</summary>
        public string Title
        {
            get => titleLabel.text;
            set => titleLabel.text = value;
        }

        public LobbyWidgetPreviewElement(LobbyWidgetId widgetId, string title)
        {
            WidgetId = widgetId;

            AddToClassList(USS_BLOCK);
            AddToClassList(FamilyClass(widgetId.Family()));
            EnableInClassList(USS_NARROW, IsNarrow(widgetId));
            pickingMode = PickingMode.Ignore;
            Vector2 size = LobbyGridElement.SpanToSize(widgetId.Span());
            style.width = size.x;
            style.height = size.y;

            titleLabel = new Label(title) { name = "Title", pickingMode = PickingMode.Ignore };
            titleLabel.AddToClassList(USS_TITLE);
            Add(titleLabel);

            var cards = new VisualElement { name = "Cards", pickingMode = PickingMode.Ignore };
            cards.AddToClassList(USS_CARDS);
            Add(cards);

            int cardCount = widgetId.CardCount();

            for (var i = 0; i < cardCount; i++)
            {
                var card = new VisualElement { name = "Card", pickingMode = PickingMode.Ignore };
                card.AddToClassList(USS_CARD);
                cards.Add(card);
            }

            if (!HasPager(widgetId))
                return;

            var pager = new VisualElement { name = "Pager", pickingMode = PickingMode.Ignore };
            pager.AddToClassList(USS_PAGER);
            Add(pager);

            for (var i = 0; i < PAGER_DOTS; i++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList(USS_PAGER_DOT);
                dot.EnableInClassList(USS_PAGER_DOT_SELECTED, i == 0);
                pager.Add(dot);
            }
        }

        // The widgets with a rail page; the recent places are a plain row and the hero and the go-to field a single entry
        private static bool HasPager(LobbyWidgetId widgetId) =>
            widgetId.Family() switch
            {
                LobbyWidgetFamily.LiveEvents => true,
                LobbyWidgetFamily.UpcomingEvents => true,
                LobbyWidgetFamily.FriendsOnline => true,
                LobbyWidgetFamily.RecommendedPlaces => true,
                LobbyWidgetFamily.Communities => true,
                LobbyWidgetFamily.Gallery => true,
                _ => false,
            };

        // The four-column sizes of the seven-column families show narrower cards (see LobbyDocument.uss)
        private static bool IsNarrow(LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.JumpBackIn4By2 => true,
                LobbyWidgetId.RecommendedPlaces4By2 => true,
                LobbyWidgetId.Communities4By2 => true,
                LobbyWidgetId.Gallery4By2 => true,
                _ => false,
            };

        private static string FamilyClass(LobbyWidgetFamily family) =>
            family switch
            {
                LobbyWidgetFamily.Landing => USS_LANDING,
                LobbyWidgetFamily.LiveEvents => USS_LIVE_EVENTS,
                LobbyWidgetFamily.UpcomingEvents => USS_UPCOMING_EVENTS,
                LobbyWidgetFamily.FriendsOnline => USS_FRIENDS,
                LobbyWidgetFamily.JumpBackIn => USS_PLACES,
                LobbyWidgetFamily.RecommendedPlaces => USS_PLACES,
                LobbyWidgetFamily.Communities => USS_PLACES,
                LobbyWidgetFamily.Gallery => USS_GALLERY,
                LobbyWidgetFamily.GoTo => USS_GO_TO,
                _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
            };
    }
}
