using System;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.Lobby
{
    /// <summary>
    ///     Card of the widget modal for a widget family off the grids: the scaled-down preview of its first size over its
    ///     title. A family of one size states the size beside the title; one of several sizes shows a button per size in
    ///     its bottom-right corner while hovered, and a drag off a button carries that size, while a drag off the card
    ///     itself carries the first one. The card reports its drags with itself as the source. Positions are in panel space.
    /// </summary>
    public class LobbyWidgetCardElement : VisualElement
    {
        /// <summary>Fraction of the size of the widget its preview is drawn at in the card.</summary>
        public const float PREVIEW_SCALE = 0.4f;

        private const string USS_BLOCK = "lobby-widget-card";
        private const string USS_PREVIEW_SLOT = USS_BLOCK + "__preview-slot";
        private const string USS_FOOTER = USS_BLOCK + "__footer";
        private const string USS_TITLE = USS_BLOCK + "__title";
        private const string USS_SIZE = USS_BLOCK + "__size";
        private const string USS_SIZES = USS_BLOCK + "__sizes";
        private const string USS_SIZE_BUTTON = USS_BLOCK + "__size-button";
        private const string USS_DRAGGING = USS_BLOCK + "--dragging";

        /// <summary>The size of the family the drag carries and the panel position it started at.</summary>
        public Action<LobbyWidgetCardElement, LobbyWidgetId, Vector2>? DragStarted;

        /// <summary>Panel position and travel since the previous report.</summary>
        public Action<LobbyWidgetCardElement, Vector2, Vector2>? Dragged;
        public Action<LobbyWidgetCardElement, Vector2>? DragEnded;

        private readonly LobbyWidgetPreviewElement[] previews;
        private readonly ClickOrDragManipulator[] gestures;
        private readonly VisualElement previewSlot;

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

            // The slot holds the first preview, scaled from its top-left corner, so it takes that size
            Vector2 slotSize = LobbyGridElement.SpanToSize(variants[0].Span()) * PREVIEW_SCALE;
            previewSlot = new VisualElement { name = "PreviewSlot", pickingMode = PickingMode.Ignore };
            previewSlot.AddToClassList(USS_PREVIEW_SLOT);
            previewSlot.style.width = slotSize.x;
            previewSlot.style.height = slotSize.y;
            Add(previewSlot);

            var footer = new VisualElement { name = "Footer", pickingMode = PickingMode.Ignore };
            footer.AddToClassList(USS_FOOTER);
            Add(footer);

            var titleLabel = new Label(title) { name = "Title", pickingMode = PickingMode.Ignore };
            titleLabel.AddToClassList(USS_TITLE);
            footer.Add(titleLabel);

            // The first gesture is the card's own and carries the first size; the others belong to the size buttons
            gestures = new ClickOrDragManipulator[variants.Length == 1 ? 1 : variants.Length + 1];
            gestures[0] = NewGesture(variants[0]);
            this.AddManipulator(gestures[0]);

            if (variants.Length == 1)
            {
                Vector2Int span = variants[0].Span();
                var size = new Label($"{span.x} x {span.y}") { name = "Size", pickingMode = PickingMode.Ignore };
                size.AddToClassList(USS_SIZE);
                footer.Add(size);
            }
            else
            {
                var sizes = new VisualElement { name = "Sizes", pickingMode = PickingMode.Ignore };
                sizes.AddToClassList(USS_SIZES);
                footer.Add(sizes);

                for (var i = 0; i < variants.Length; i++)
                {
                    Vector2Int span = variants[i].Span();
                    var button = new Label($"{span.x}x{span.y}") { name = variants[i].ToString() };
                    button.AddToClassList(USS_SIZE_BUTTON);
                    // A press on a size button must not reach the gesture of the card, or both would drag
                    button.RegisterCallback<PointerDownEvent>(static evt => evt.StopPropagation());
                    gestures[i + 1] = NewGesture(variants[i]);
                    button.AddManipulator(gestures[i + 1]);
                    sizes.Add(button);
                }
            }

            Restore();
        }

        /// <summary>
        ///     Stand-in of the given size of the family, free to be lifted elsewhere at another scale until
        ///     <see cref="Restore" />.
        /// </summary>
        public LobbyWidgetPreviewElement Preview(LobbyWidgetId variant)
        {
            for (var i = 0; i < previews.Length; i++)
                if (previews[i].WidgetId == variant)
                    return previews[i];

            throw new ArgumentOutOfRangeException(nameof(variant), variant, $"{variant} is not a size of {Family}");
        }

        /// <summary>Puts the preview of the first size back into the slot at the card scale and the others away.</summary>
        public void Restore()
        {
            for (var i = 1; i < previews.Length; i++)
                previews[i].RemoveFromHierarchy();

            if (previews[0].parent != previewSlot)
                previewSlot.Add(previews[0]);

            previews[0].SetScale(PREVIEW_SCALE);
        }

        /// <summary>Marks the card while one of its sizes is on the move: it dims and keeps its size buttons out.</summary>
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
    }
}
