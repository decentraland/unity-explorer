using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Row of the widget modal for a widget family off the grids: a grip, the icon of the family and its title. A drag
    ///     off the row carries the first size of the family; a family of several sizes also shows a button per size at the
    ///     right end of the row while hovered, and a drag off a button carries that size. The row keeps a stand-in of each
    ///     size for the drag to lift (see <see cref="Preview" />) and reports its drags with itself as the source.
    ///     Positions are in panel space.
    /// </summary>
    public class LobbyWidgetCardElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-widget-card";
        private const string USS_GRIP = USS_BLOCK + "__grip";
        private const string USS_GRIP_COLUMN = USS_BLOCK + "__grip-column";
        private const string USS_ICON = USS_BLOCK + "__icon";
        private const string USS_ICON_LANDING = USS_ICON + "--landing";
        private const string USS_ICON_LIVE_EVENTS = USS_ICON + "--live-events";
        private const string USS_ICON_UPCOMING_EVENTS = USS_ICON + "--upcoming-events";
        private const string USS_ICON_FRIENDS_ONLINE = USS_ICON + "--friends-online";
        private const string USS_ICON_JUMP_BACK_IN = USS_ICON + "--jump-back-in";
        private const string USS_ICON_RECOMMENDED_PLACES = USS_ICON + "--recommended-places";
        private const string USS_ICON_COMMUNITIES = USS_ICON + "--communities";
        private const string USS_ICON_GO_TO = USS_ICON + "--go-to";
        private const string USS_TITLE = USS_BLOCK + "__title";
        private const string USS_SIZES = USS_BLOCK + "__sizes";
        private const string USS_SIZE_BUTTON = USS_BLOCK + "__size-button";
        private const string USS_DRAGGING = USS_BLOCK + "--dragging";

        private const int GRIP_COLUMNS = 2;

        /// <summary>The size of the family the drag carries and the panel position it started at.</summary>
        public Action<LobbyWidgetCardElement, LobbyWidgetId, Vector2>? DragStarted;

        /// <summary>Panel position and travel since the previous report.</summary>
        public Action<LobbyWidgetCardElement, Vector2, Vector2>? Dragged;
        public Action<LobbyWidgetCardElement, Vector2>? DragEnded;

        private readonly LobbyWidgetPreviewElement[] previews;
        private readonly ClickOrDragManipulator[] gestures;

        public LobbyWidgetFamily Family { get; }

        public LobbyWidgetCardElement(LobbyWidgetFamily family)
        {
            Family = family;
            ReadOnlySpan<LobbyWidgetId> variants = family.Variants();
            string title = family.Title();

            AddToClassList(USS_BLOCK);
            AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);

            previews = new LobbyWidgetPreviewElement[variants.Length];

            for (var i = 0; i < variants.Length; i++)
                previews[i] = new LobbyWidgetPreviewElement(variants[i], title) { name = "Preview" };

            var grip = new VisualElement { name = "Grip", pickingMode = PickingMode.Ignore };
            grip.AddToClassList(USS_GRIP);

            for (var i = 0; i < GRIP_COLUMNS; i++)
            {
                var column = new VisualElement { pickingMode = PickingMode.Ignore };
                column.AddToClassList(USS_GRIP_COLUMN);
                grip.Add(column);
            }

            Add(grip);

            var icon = new VisualElement { name = "Icon", pickingMode = PickingMode.Ignore };
            icon.AddToClassList(USS_ICON);
            icon.AddToClassList(IconClass(family));
            Add(icon);

            var titleLabel = new Label(title) { name = "Title", pickingMode = PickingMode.Ignore };
            titleLabel.AddToClassList(USS_TITLE);
            Add(titleLabel);

            // The first gesture is the row's own and carries the first size; the others belong to the size buttons
            gestures = new ClickOrDragManipulator[variants.Length == 1 ? 1 : variants.Length + 1];
            gestures[0] = NewGesture(variants[0]);
            this.AddManipulator(gestures[0]);

            if (variants.Length == 1)
                return;

            var sizes = new VisualElement { name = "Sizes", pickingMode = PickingMode.Ignore };
            sizes.AddToClassList(USS_SIZES);
            Add(sizes);

            // A size reads as the cards it shows, the smaller first
            for (var i = 0; i < variants.Length; i++)
            {
                int cardCount = variants[i].CardCount();
                var button = new Label($"x{cardCount}") { name = variants[i].ToString(), userData = cardCount };
                button.AddToClassList(USS_SIZE_BUTTON);
                // A press on a size button must not reach the gesture of the row, or both would drag
                button.RegisterCallback<PointerDownEvent>(static evt => evt.StopPropagation());
                gestures[i + 1] = NewGesture(variants[i]);
                button.AddManipulator(gestures[i + 1]);

                var at = 0;

                while (at < sizes.childCount && (int)sizes[at].userData < cardCount)
                    at++;

                sizes.Insert(at, button);
            }
        }

        /// <summary>
        ///     Stand-in of the given size of the family, free to be lifted elsewhere until <see cref="Restore" />.
        /// </summary>
        public LobbyWidgetPreviewElement Preview(LobbyWidgetId variant)
        {
            for (var i = 0; i < previews.Length; i++)
                if (previews[i].WidgetId == variant)
                    return previews[i];

            throw new ArgumentOutOfRangeException(nameof(variant), variant, $"{variant} is not a size of {Family}");
        }

        /// <summary>Puts every stand-in away.</summary>
        public void Restore()
        {
            for (var i = 0; i < previews.Length; i++)
                previews[i].RemoveFromHierarchy();
        }

        /// <summary>Marks the row while one of its sizes is on the move: it dims and keeps its size buttons out.</summary>
        public void SetDragging(bool dragging) =>
            EnableInClassList(USS_DRAGGING, dragging);

        /// <summary>Forgets a press in progress; a drag ends where the pointer last was.</summary>
        public void Cancel()
        {
            for (var i = 0; i < gestures.Length; i++)
                gestures[i].Cancel();
        }

        private ClickOrDragManipulator NewGesture(LobbyWidgetId variant) =>
            new ()
            {
                DragStarted = position => DragStarted?.Invoke(this, variant, position),
                Dragged = (position, delta) => Dragged?.Invoke(this, position, delta),
                DragEnded = position => DragEnded?.Invoke(this, position),
            };

        private static string IconClass(LobbyWidgetFamily family) =>
            family switch
            {
                LobbyWidgetFamily.Landing => USS_ICON_LANDING,
                LobbyWidgetFamily.LiveEvents => USS_ICON_LIVE_EVENTS,
                LobbyWidgetFamily.UpcomingEvents => USS_ICON_UPCOMING_EVENTS,
                LobbyWidgetFamily.FriendsOnline => USS_ICON_FRIENDS_ONLINE,
                LobbyWidgetFamily.JumpBackIn => USS_ICON_JUMP_BACK_IN,
                LobbyWidgetFamily.RecommendedPlaces => USS_ICON_RECOMMENDED_PLACES,
                LobbyWidgetFamily.Communities => USS_ICON_COMMUNITIES,
                LobbyWidgetFamily.GoTo => USS_ICON_GO_TO,
                _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
            };
    }
}
