using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Edit-mode cover of a placed widget that takes the pointer and reports its drags with itself as the source. It
    ///     spans the cells of the widget over its section, with its title in the middle and a button in its top-right
    ///     corner that asks for the widget to leave its grid. Positions are in panel space.
    /// </summary>
    public class LobbyWidgetHandleElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-widget-handle";
        private const string USS_LABEL = USS_BLOCK + "__label";
        private const string USS_REMOVE = USS_BLOCK + "__remove";
        private const string USS_REMOVE_ICON = USS_BLOCK + "__remove-icon";
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

        public LobbyWidgetHandleElement(LobbyWidgetId widgetId, string title)
        {
            WidgetId = widgetId;

            AddToClassList(USS_BLOCK);
            AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);

            Vector2 size = LobbyGridElement.SpanToSize(widgetId.Span());
            style.width = size.x;
            style.height = size.y;

            var label = new Label(title) { name = "Label", pickingMode = PickingMode.Ignore };
            label.AddToClassList(USS_LABEL);
            Add(label);

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
        }

        /// <summary>Hides the handle while its widget is on the move, so its cells show clear.</summary>
        public void SetDragging(bool dragging) =>
            EnableInClassList(USS_DRAGGING, dragging);

        /// <summary>
        ///     Hides the handle while its widget stands aside for a drag hovering over it, so the widget slides alone and
        ///     no card trails it.
        /// </summary>
        public void SetPushed(bool pushed) =>
            EnableInClassList(USS_PUSHED, pushed);

        /// <summary>Forgets a press in progress; a drag ends where the pointer last was.</summary>
        public void Cancel() =>
            gesture.Cancel();
    }
}
