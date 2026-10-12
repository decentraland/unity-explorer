using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Edit-mode cover of a placed widget: it spans the cells of the widget over its section, takes the pointer instead
    ///     of the section and reports its drags with itself as the source. Positions are in panel space.
    /// </summary>
    public class LobbyWidgetHandleElement : VisualElement
    {
        private const string USS_BLOCK = "lobby-widget-handle";
        private const string USS_LABEL = USS_BLOCK + "__label";
        private const string USS_DRAGGING = USS_BLOCK + "--dragging";

        public Action<LobbyWidgetHandleElement, Vector2>? DragStarted;

        /// <summary>Panel position and travel since the previous report.</summary>
        public Action<LobbyWidgetHandleElement, Vector2, Vector2>? Dragged;
        public Action<LobbyWidgetHandleElement, Vector2>? DragEnded;

        private readonly ClickOrDragManipulator gesture;

        public LobbyWidgetId WidgetId { get; }

        /// <summary>Name of the widget shown on the handle.</summary>
        public string Title { get; }

        public LobbyWidgetHandleElement(LobbyWidgetId widgetId, string title)
        {
            WidgetId = widgetId;
            Title = title;

            AddToClassList(USS_BLOCK);
            AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);
            Vector2 size = LobbyGridElement.SpanToSize(widgetId.Span());
            style.width = size.x;
            style.height = size.y;

            var label = new Label(title) { name = "Label", pickingMode = PickingMode.Ignore };
            label.AddToClassList(USS_LABEL);
            Add(label);

            gesture = new ClickOrDragManipulator
            {
                DragStarted = position => DragStarted?.Invoke(this, position),
                Dragged = (position, delta) => Dragged?.Invoke(this, position, delta),
                DragEnded = position => DragEnded?.Invoke(this, position),
            };

            this.AddManipulator(gesture);
        }

        /// <summary>Dims the handle while its widget is on the move, so the origin stays visible.</summary>
        public void SetDragging(bool dragging) =>
            EnableInClassList(USS_DRAGGING, dragging);

        /// <summary>Forgets a press in progress; a drag ends where the pointer last was.</summary>
        public void Cancel() =>
            gesture.Cancel();
    }
}
