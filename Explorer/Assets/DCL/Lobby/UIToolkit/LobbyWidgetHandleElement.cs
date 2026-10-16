using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Edit-mode frame of a placed widget that takes the pointer and reports its drags with itself as the source. It
    ///     covers the cells of the widget over its section (whose content stays inset from them), with a button in its
    ///     top-right corner that asks for the widget to leave its grid. Its tint goes in a separate <see cref="Underlay" />
    ///     meant to sit under the section, so the widget itself is not tinted. Positions are in panel space.
    /// </summary>
    public class LobbyWidgetHandleElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-widget-handle";
        private const string USS_REMOVE = USS_BLOCK + "__remove";
        private const string USS_REMOVE_ICON = USS_BLOCK + "__remove-icon";
        private const string USS_UNDERLAY = USS_BLOCK + "__underlay";
        private const string USS_UNDERLAY_HOVERED = USS_UNDERLAY + "--hovered";
        private const string USS_DRAGGING = USS_BLOCK + "--dragging";
        private const string USS_PUSHED = USS_BLOCK + "--pushed";

        public Action<LobbyWidgetHandleElement, Vector2>? DragStarted;

        /// <summary>Panel position and travel since the previous report.</summary>
        public Action<LobbyWidgetHandleElement, Vector2, Vector2>? Dragged;
        public Action<LobbyWidgetHandleElement, Vector2>? DragEnded;

        /// <summary>The button in the corner of the handle was clicked: the widget is to leave its grid.</summary>
        public Action<LobbyWidgetHandleElement>? RemoveRequested;

        private readonly ClickOrDragManipulator gesture;

        public LobbyWidgetId WidgetId { get; }

        /// <summary>
        ///     The tint of the handle, the size of the handle and placed with it, for the hierarchy to put under the
        ///     section; it hides along with the handle.
        /// </summary>
        public VisualElement Underlay { get; }

        /// <summary>Panel position of the top-left corner of the cells of the widget, where the frame starts.</summary>
        public Vector2 WidgetPosition => worldBound.position;

        public LobbyWidgetHandleElement(LobbyWidgetId widgetId)
        {
            WidgetId = widgetId;

            AddToClassList(USS_BLOCK);
            AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);

            Vector2 size = LobbyGridElement.SpanToSize(widgetId.Span());
            style.width = size.x;
            style.height = size.y;

            Underlay = new VisualElement { name = "WidgetUnderlay", pickingMode = PickingMode.Ignore };
            Underlay.AddToClassList(USS_UNDERLAY);
            Underlay.style.width = style.width;
            Underlay.style.height = style.height;

            var remove = new Button(() => RemoveRequested?.Invoke(this)) { name = "Remove" };
            remove.AddToClassList(USS_REMOVE);
            // A press on the button must not reach the gesture below, or it would start a drag
            remove.RegisterCallback<PointerDownEvent>(static evt => evt.StopPropagation());
            var removeIcon = new VisualElement { name = "Icon", pickingMode = PickingMode.Ignore };
            removeIcon.AddToClassList(USS_REMOVE_ICON);
            remove.Add(removeIcon);
            Add(remove);

            gesture = new ClickOrDragManipulator
            {
                DragStarted = position => DragStarted?.Invoke(this, position),
                Dragged = (position, delta) => Dragged?.Invoke(this, position, delta),
                DragEnded = position => DragEnded?.Invoke(this, position),
            };

            this.AddManipulator(gesture);

            // The underlay is no descendant of the frame, so the hover of the frame is passed on to it by hand
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        private void OnPointerEnter(PointerEnterEvent _) =>
            Underlay.AddToClassList(USS_UNDERLAY_HOVERED);

        private void OnPointerLeave(PointerLeaveEvent _) =>
            Underlay.RemoveFromClassList(USS_UNDERLAY_HOVERED);

        /// <summary>Puts the frame over the cells of the widget at the cell, in the space of its grid.</summary>
        public void PlaceAt(Vector2Int cell)
        {
            Vector2 local = LobbyGridElement.CellToLocal(cell);
            style.left = local.x;
            style.top = local.y;
            Underlay.style.left = style.left;
            Underlay.style.top = style.top;
        }

        /// <summary>Hides the handle while its widget is on the move, so its cells show clear.</summary>
        public void SetDragging(bool dragging)
        {
            EnableInClassList(USS_DRAGGING, dragging);
            Underlay.EnableInClassList(USS_DRAGGING, dragging);
        }

        /// <summary>
        ///     Hides the handle while its widget stands aside for a drag hovering over it, so the widget slides alone and
        ///     no frame trails it.
        /// </summary>
        public void SetPushed(bool pushed)
        {
            EnableInClassList(USS_PUSHED, pushed);
            Underlay.EnableInClassList(USS_PUSHED, pushed);
        }

        /// <summary>Forgets a press in progress; a drag ends where the pointer last was.</summary>
        public void Cancel() =>
            gesture.Cancel();
    }
}