using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Edit-mode stand-in of a widget that takes the pointer and reports its drags with itself as the source. Placed,
    ///     it spans the cells of the widget over its section, with a button in its top-right corner that asks for the
    ///     widget to leave its grid; shelved, it is a row of the widget modal that shows the scaled-down preview of the
    ///     widget beside its title and its span. Positions are in panel space.
    /// </summary>
    public class LobbyWidgetHandleElement : VisualElement
    {
        /// <summary>Fraction of the size of the widget its preview is drawn at in the row.</summary>
        public const float PREVIEW_SCALE = 0.25f;

        private const string USS_BLOCK = "lobby-widget-handle";
        private const string USS_PREVIEW_SLOT = USS_BLOCK + "__preview-slot";
        private const string USS_TEXT = USS_BLOCK + "__text";
        private const string USS_LABEL = USS_BLOCK + "__label";
        private const string USS_SIZE = USS_BLOCK + "__size";
        private const string USS_REMOVE = USS_BLOCK + "__remove";
        private const string USS_REMOVE_ICON = USS_BLOCK + "__remove-icon";
        private const string USS_DRAGGING = USS_BLOCK + "--dragging";
        private const string USS_PUSHED = USS_BLOCK + "--pushed";
        private const string USS_SHELVED = USS_BLOCK + "--shelved";

        /// <summary>The slot takes the widest preview so the text column lines up across the rows.</summary>
        private static readonly float PREVIEW_SLOT_WIDTH = WidestSpan() * PREVIEW_SCALE;

        public Action<LobbyWidgetHandleElement, Vector2>? DragStarted;

        /// <summary>Panel position and travel since the previous report.</summary>
        public Action<LobbyWidgetHandleElement, Vector2, Vector2>? Dragged;
        public Action<LobbyWidgetHandleElement, Vector2>? DragEnded;

        /// <summary>The button in the corner of a placed handle was clicked: the widget is to leave its grid.</summary>
        public Action<LobbyWidgetHandleElement>? RemoveRequested;

        private readonly ClickOrDragManipulator gesture;
        private readonly VisualElement previewSlot;

        public LobbyWidgetId WidgetId { get; }

        /// <summary>Name of the widget shown on the handle.</summary>
        public string Title { get; }

        /// <summary>
        ///     Stand-in of the widget: in the slot of the row while the handle is shelved, free to be lifted elsewhere at
        ///     another scale, and out of the hierarchy while the handle is placed.
        /// </summary>
        public LobbyWidgetPreviewElement Preview { get; }

        /// <summary>Whether the handle is a row of the widget modal rather than the cover of a placed widget.</summary>
        public bool IsShelved { get; private set; }

        public LobbyWidgetHandleElement(LobbyWidgetId widgetId, string title)
        {
            WidgetId = widgetId;
            Title = title;
            Preview = new LobbyWidgetPreviewElement(widgetId, title) { name = "Preview" };

            AddToClassList(USS_BLOCK);
            AddToClassList(VisualElementsExtensions.INTERACTABLE_CLASS);

            Vector2 previewSize = LobbyGridElement.SpanToSize(widgetId.Span()) * PREVIEW_SCALE;
            previewSlot = new VisualElement { name = "PreviewSlot", pickingMode = PickingMode.Ignore };
            previewSlot.AddToClassList(USS_PREVIEW_SLOT);
            previewSlot.style.width = PREVIEW_SLOT_WIDTH;
            previewSlot.style.height = previewSize.y;
            Add(previewSlot);

            var text = new VisualElement { name = "Text", pickingMode = PickingMode.Ignore };
            text.AddToClassList(USS_TEXT);
            Add(text);

            var label = new Label(title) { name = "Label", pickingMode = PickingMode.Ignore };
            label.AddToClassList(USS_LABEL);
            text.Add(label);

            Vector2Int span = widgetId.Span();
            var size = new Label($"{span.x} x {span.y}") { name = "Size", pickingMode = PickingMode.Ignore };
            size.AddToClassList(USS_SIZE);
            text.Add(size);

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
            SetShelved(false);
        }

        /// <summary>
        ///     Shelved, the handle flows in the list of the modal at the size its stylesheet gives it and holds the preview
        ///     scaled down in its slot; placed, it is as big as the span of the widget, sits where it is moved to and lets
        ///     the preview go.
        /// </summary>
        public void SetShelved(bool shelved)
        {
            IsShelved = shelved;
            EnableInClassList(USS_SHELVED, shelved);

            if (shelved)
            {
                style.width = StyleKeyword.Null;
                style.height = StyleKeyword.Null;
                style.left = StyleKeyword.Null;
                style.top = StyleKeyword.Null;

                if (Preview.parent != previewSlot)
                    previewSlot.Add(Preview);

                Preview.SetScale(PREVIEW_SCALE);
                return;
            }

            Preview.RemoveFromHierarchy();
            Vector2 size = LobbyGridElement.SpanToSize(WidgetId.Span());
            style.width = size.x;
            style.height = size.y;
        }

        /// <summary>Marks the handle while its widget is on the move: placed, it hides; shelved, its row dims.</summary>
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

        private static float WidestSpan()
        {
            var widgets = (LobbyWidgetId[])Enum.GetValues(typeof(LobbyWidgetId));
            var widest = 0f;

            for (var i = 0; i < widgets.Length; i++)
                widest = Mathf.Max(widest, LobbyGridElement.SpanToSize(widgets[i].Span()).x);

            return widest;
        }
    }
}
