using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Edit mode of the lobby layout: every placed widget gets a handle framing its cells that can be dragged onto
    ///     other cells, and every widget family with none of its sizes placed a row in the widget modal over the avatar
    ///     that can be dragged onto a grid, at the size picked on the row. The widget itself, or the stand-in of an
    ///     unplaced one, follows the pointer on the drag layer in a frame of its own, its cells left clear behind it. A
    ///     drop takes only when the whole widget fits inside one grid; the widgets in its way make room by moving off the
    ///     side it covers, and they slide to where they would go, without their handles, while the drag hovers over them.
    ///     A release of a placed widget with the pointer over the modal takes it off its grid, as does the button in the
    ///     corner of its handle; <see cref="LobbyLayout.REQUIRED_WIDGET" /> has no such button and never targets the
    ///     modal. Any other release leaves every widget where it was. Every change shows at once but is
    ///     stored only by the Save button of the modal, which leaves edit mode; Cancel and the Back button of the header,
    ///     like <see cref="Hide" />, put the layout back as it was on entry, and Reset to Default puts the shipped layout
    ///     up for saving.
    /// </summary>
    public class LobbyLayoutEditor
    {
        private const string DRAG_LAYER_NAME = "LayoutDragLayer";
        private const string MODAL_NAME = "WidgetModal";
        private const string MODAL_ITEMS_NAME = "WidgetModalItems";
        private const string RESET_BUTTON_NAME = "ResetLayout";
        private const string CANCEL_BUTTON_NAME = "CancelLayout";
        private const string BACK_BUTTON_NAME = "BackFromLayout";
        private const string SAVE_BUTTON_NAME = "SaveLayout";
        private const string USS_EDITING = "lobby--editing";
        private const string USS_WIDGET_LIFTED = "lobby__widget--lifted";
        private const string USS_GRID_SHIELD = "lobby-grid-shield";
        private const string USS_DRAG_FRAME = "lobby-drag-frame";
        private const string USS_DROP_MARKER = "lobby-drop-marker";
        private const string USS_DROP_MARKER_VALID = USS_DROP_MARKER + "--valid";
        private const string USS_DROP_MARKER_INVALID = USS_DROP_MARKER + "--invalid";
        private const string USS_MODAL_TARGET = "lobby__widget-modal--target";

        private static readonly LobbyWidgetId[] WIDGETS = (LobbyWidgetId[])Enum.GetValues(typeof(LobbyWidgetId));
        private static readonly LobbyWidgetFamily[] FAMILIES = (LobbyWidgetFamily[])Enum.GetValues(typeof(LobbyWidgetFamily));
        private static readonly LobbyGridCorner[] CORNERS = (LobbyGridCorner[])Enum.GetValues(typeof(LobbyGridCorner));

        private readonly LobbyWidgetHost host;
        private readonly LobbyWidgetHandleElement[] handles = new LobbyWidgetHandleElement[WIDGETS.Length];
        private readonly LobbyWidgetCardElement[] cards = new LobbyWidgetCardElement[FAMILIES.Length];

        /// <summary>One per grid, between its sections and its handles, so no widget hears the pointer while edited.</summary>
        private readonly VisualElement[] shields = new VisualElement[CORNERS.Length];

        /// <summary>Under the lifted widget on the drag layer, framing its cells like a handle does.</summary>
        private readonly VisualElement dragFrame;
        private readonly VisualElement dropMarker;

        /// <summary>The placements on entry: Cancel and Hide put them back, Save measures the changes against them.</summary>
        private readonly List<LobbyWidgetPlacement> opening = new ();

        /// <summary>Where the widgets in the way of the drop stand while the drag hovers over them.</summary>
        private readonly List<LobbyWidgetPlacement> pushed = new ();
        private readonly List<LobbyWidgetPlacement> resolution = new ();

        private VisualElement? root;
        private VisualElement? dragLayer;
        private VisualElement? modal;
        private VisualElement? modalItems;
        private Button? resetButton;
        private Button? cancelButton;
        private Button? backButton;
        private Button? saveButton;

        /// <summary>The root of the panel, where a key press lands while nothing is focused and bubbles up otherwise.</summary>
        private VisualElement? panelRoot;

        /// <summary>The source of the drag in progress: the handle of a placed widget or the row of an unplaced family.</summary>
        private LobbyWidgetHandleElement? draggedHandle;
        private LobbyWidgetCardElement? draggedCard;
        private LobbyWidgetId draggedWidgetId;

        /// <summary>The section or the stand-in of the dragged widget while it follows the pointer on the drag layer.</summary>
        private VisualElement? lifted;

        /// <summary>Panel pixels from the top-left corner of the lifted widget to the pointer.</summary>
        private Vector2 grabOffset;
        private LobbyGridElement? targetGrid;
        private Vector2Int targetCell;
        private bool targetValid;
        private bool targetModal;

        public bool IsEditing => root != null;

        private LobbyLayout RequireLayout() => host.Layout ?? throw new InvalidOperationException("The layout can be edited only once the widget host is shown");

        private VisualElement RequireDragLayer() => dragLayer ?? throw new InvalidOperationException("The layout can be dragged only while the editor is shown");

        private VisualElement RequireModal() => modal ?? throw new InvalidOperationException("The widget modal exists only while the editor is shown");

        private VisualElement RequireModalItems() => modalItems ?? throw new InvalidOperationException("The widget modal exists only while the editor is shown");

        public LobbyLayoutEditor(LobbyWidgetHost host)
        {
            this.host = host;

            for (var i = 0; i < WIDGETS.Length; i++)
            {
                LobbyWidgetId widgetId = WIDGETS[i];

                handles[(int)widgetId] = new LobbyWidgetHandleElement(widgetId, removable: widgetId != LobbyLayout.REQUIRED_WIDGET)
                {
                    name = widgetId.ToString(),
                    DragStarted = OnHandleDragStarted,
                    Dragged = OnHandleDragged,
                    DragEnded = OnHandleDragEnded,
                    RemoveRequested = OnRemoveRequested,
                };
            }

            for (var i = 0; i < FAMILIES.Length; i++)
            {
                LobbyWidgetFamily family = FAMILIES[i];

                cards[(int)family] = new LobbyWidgetCardElement(family)
                {
                    name = family.ToString(),
                    DragStarted = OnCardDragStarted,
                    Dragged = OnCardDragged,
                    DragEnded = OnCardDragEnded,
                };
            }

            for (var i = 0; i < CORNERS.Length; i++)
            {
                shields[i] = new VisualElement { name = "GridShield" };
                shields[i].AddToClassList(USS_GRID_SHIELD);
            }

            dragFrame = new VisualElement { name = "DragFrame", pickingMode = PickingMode.Ignore };
            dragFrame.AddToClassList(USS_DRAG_FRAME);

            dropMarker = new VisualElement { name = "DropMarker", pickingMode = PickingMode.Ignore };
            dropMarker.AddToClassList(USS_DROP_MARKER);
        }

        /// <summary>
        ///     Enters edit mode on the hierarchy the widget host was last shown with: the grids show their backdrop and
        ///     anchor dots, a handle frames every placed widget and the modal lists every family with none of its sizes
        ///     placed, with nothing to save yet.
        /// </summary>
        public void Show(VisualElement newRoot)
        {
            if (root != null)
                Hide();

            root = newRoot;
            dragLayer = Find<VisualElement>(newRoot, DRAG_LAYER_NAME);
            modal = Find<VisualElement>(newRoot, MODAL_NAME);
            modalItems = Find<VisualElement>(newRoot, MODAL_ITEMS_NAME);
            resetButton = Find<Button>(newRoot, RESET_BUTTON_NAME);
            cancelButton = Find<Button>(newRoot, CANCEL_BUTTON_NAME);
            backButton = Find<Button>(newRoot, BACK_BUTTON_NAME);
            saveButton = Find<Button>(newRoot, SAVE_BUTTON_NAME);
            resetButton.clicked += ResetToDefault;
            cancelButton.clicked += Hide;
            backButton.clicked += Hide;
            saveButton.clicked += Save;
            panelRoot = newRoot.panel.visualTree;
            panelRoot.RegisterCallback<KeyDownEvent>(OnKeyDown);
            panelRoot.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);

            opening.Clear();
            opening.AddRange(RequireLayout().Placements);

            newRoot.AddToClassList(USS_EDITING);

            for (var i = 0; i < CORNERS.Length; i++)
                host.Grid(CORNERS[i]).SetEditing(true);

            PlaceHandles();
            UpdateSaveButton();
        }

        /// <summary>
        ///     Leaves edit mode without storing: the layout goes back to what it was on entry. A drag in progress is dropped
        ///     where the widget came from.
        /// </summary>
        public void Hide() =>
            Close(revert: true);

        // The shield of each grid goes in after the sections so it takes the pointer from whatever they hang outside
        // their cells, and the placed handles after it so they draw over everything and take the pointer first, each
        // with its underlay right under its section; the rows of the unplaced families line up in the modal in the
        // order of the families
        private void PlaceHandles()
        {
            for (var i = 0; i < CORNERS.Length; i++)
            {
                shields[i].RemoveFromHierarchy();
                host.Grid(CORNERS[i]).Add(shields[i]);
            }

            for (var i = 0; i < handles.Length; i++)
            {
                LobbyWidgetHandleElement handle = handles[i];
                handle.RemoveFromHierarchy();
                handle.Underlay.RemoveFromHierarchy();

                if (!RequireLayout().TryGetPlacement(handle.WidgetId, out LobbyWidgetPlacement placement))
                    continue;

                handle.PlaceAt(placement.Cell);
                LobbyGridElement grid = host.Grid(placement.Corner);
                grid.Insert(grid.IndexOf(host.Section(handle.WidgetId)), handle.Underlay);
                grid.Add(handle);
            }

            for (var i = 0; i < cards.Length; i++)
            {
                LobbyWidgetCardElement card = cards[i];
                card.RemoveFromHierarchy();
                card.Restore();

                if (!IsPlaced(card.Family))
                    RequireModalItems().Add(card);
            }
        }

        private bool IsPlaced(LobbyWidgetFamily family)
        {
            ReadOnlySpan<LobbyWidgetId> variants = family.Variants();

            for (var i = 0; i < variants.Length; i++)
                if (RequireLayout().TryGetPlacement(variants[i], out _))
                    return true;

            return false;
        }

        // Stores the layout as it stands and leaves edit mode; a drag in progress is cancelled first, its widget staying
        // where it came from
        private void Save()
        {
            EndDragInProgress();
            LobbyLayoutStorage.Save(RequireLayout());
            Close(revert: false);
        }

        // The shipped layout goes up like any other change: it stands until saved
        private void ResetToDefault()
        {
            EndDragInProgress();
            RequireLayout().ResetToDefault();
            host.Apply();
            PlaceHandles();
            UpdateSaveButton();
        }

        private void Close(bool revert)
        {
            if (root == null)
                return;

            EndDragInProgress();

            if (revert)
            {
                RestoreOpening();
                host.Apply();
            }

            for (var i = 0; i < handles.Length; i++)
            {
                handles[i].RemoveFromHierarchy();
                handles[i].Underlay.RemoveFromHierarchy();
            }

            for (var i = 0; i < cards.Length; i++)
                cards[i].RemoveFromHierarchy();

            for (var i = 0; i < shields.Length; i++)
                shields[i].RemoveFromHierarchy();

            dropMarker.RemoveFromHierarchy();

            for (var i = 0; i < CORNERS.Length; i++)
            {
                LobbyGridElement grid = host.Grid(CORNERS[i]);
                grid.SetEditing(false);
                grid.SetHighlighted(false);
                grid.ClearDropHint();
            }

            resetButton!.clicked -= ResetToDefault;
            cancelButton!.clicked -= Hide;
            backButton!.clicked -= Hide;
            saveButton!.clicked -= Save;
            panelRoot!.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            panelRoot.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            panelRoot = null;
            root.RemoveFromClassList(USS_EDITING);
            root = null;
            dragLayer = null;
            modal = null;
            modalItems = null;
            resetButton = null;
            cancelButton = null;
            backButton = null;
            saveButton = null;
        }

        // Escape leaves like the Back and Cancel buttons; it comes as a key press and, from the input module, as a
        // navigation cancel, and the second of the two finds the editor already closed
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
                return;

            evt.StopPropagation();
            Hide();
        }

        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            Hide();
        }

        private void RestoreOpening()
        {
            RequireLayout().Clear();

            for (var i = 0; i < opening.Count; i++)
                RequireLayout().TryPlace(opening[i].WidgetId, opening[i].Corner, opening[i].Cell);
        }

        /// <summary>Whether any widget stands elsewhere than it did on entry.</summary>
        private bool HasChanges()
        {
            if (RequireLayout().Placements.Count != opening.Count)
                return true;

            for (var i = 0; i < opening.Count; i++)
            {
                LobbyWidgetPlacement was = opening[i];

                if (!RequireLayout().TryGetPlacement(was.WidgetId, out LobbyWidgetPlacement now) || now.Corner != was.Corner || now.Cell != was.Cell)
                    return true;
            }

            return false;
        }

        private void UpdateSaveButton() =>
            saveButton?.SetEnabled(HasChanges());

        // The section of a placed widget is lifted keeping the pointer where it was grabbed
        private void OnHandleDragStarted(LobbyWidgetHandleElement handle, Vector2 panelPosition)
        {
            draggedHandle = handle;
            handle.SetDragging(true);
            BeginDrag(handle.WidgetId, host.Section(handle.WidgetId), panelPosition - handle.WidgetPosition, panelPosition);
        }

        // The stand-in of the picked size is lifted hanging from its middle, as the row is far smaller than the widget
        private void OnCardDragStarted(LobbyWidgetCardElement card, LobbyWidgetId variant, Vector2 panelPosition)
        {
            draggedCard = card;
            card.SetDragging(true);
            BeginDrag(variant, card.Preview(variant), LobbyGridElement.SpanToSize(variant.Span()) * 0.5f, panelPosition);
        }

        // The lifted element goes onto the drag layer so it draws over every grid, after the frame that goes under it,
        // and its slide transition is switched off so it keeps up with the pointer; the reparent itself runs no
        // transition
        private void BeginDrag(LobbyWidgetId widgetId, VisualElement element, Vector2 grab, Vector2 panelPosition)
        {
            draggedWidgetId = widgetId;
            grabOffset = grab;
            lifted = element;

            Vector2 size = LobbyGridElement.SpanToSize(widgetId.Span());
            dropMarker.style.width = size.x;
            dropMarker.style.height = size.y;
            dragFrame.style.width = size.x + (LobbyWidgetHandleElement.OUTSET * 2f);
            dragFrame.style.height = size.y + (LobbyWidgetHandleElement.OUTSET * 2f);

            element.AddToClassList(USS_WIDGET_LIFTED);
            RequireDragLayer().Add(dragFrame);
            RequireDragLayer().Add(element);

            MoveLifted(panelPosition);
            UpdateTarget(panelPosition);
        }

        private void OnHandleDragged(LobbyWidgetHandleElement _, Vector2 panelPosition, Vector2 __) =>
            OnDragged(panelPosition);

        private void OnCardDragged(LobbyWidgetCardElement _, Vector2 panelPosition, Vector2 __) =>
            OnDragged(panelPosition);

        private void OnDragged(Vector2 panelPosition)
        {
            MoveLifted(panelPosition);
            UpdateTarget(panelPosition);
        }

        private void OnHandleDragEnded(LobbyWidgetHandleElement _, Vector2 __) =>
            EndDrag();

        private void OnCardDragEnded(LobbyWidgetCardElement _, Vector2 __) =>
            EndDrag();

        // The host lands the lifted section where the drop left it: on its grid at the cell the drop took or the one it
        // came from, or off every grid; a lifted stand-in goes away
        private void EndDrag()
        {
            if (targetModal)
                RequireLayout().Remove(draggedWidgetId);
            else if (targetValid && targetGrid != null)
                RequireLayout().TryPlacePushing(draggedWidgetId, targetGrid.Corner, targetCell);

            ClearTarget();
            lifted?.RemoveFromClassList(USS_WIDGET_LIFTED);
            lifted = null;
            dragFrame.RemoveFromHierarchy();
            host.Apply();
            draggedHandle?.SetDragging(false);
            draggedHandle = null;
            draggedCard?.SetDragging(false);
            draggedCard = null;
            PlaceHandles();
            UpdateSaveButton();
        }

        // A drag cut short is ended through the gesture it came from with no target left, so the release puts its widget
        // back where it came from
        private void EndDragInProgress()
        {
            if (draggedHandle == null && draggedCard == null)
                return;

            ClearTarget();
            draggedHandle?.Cancel();
            draggedCard?.Cancel();
        }

        // The button in the corner of a placed handle does what a drop on the modal does, without the drag
        private void OnRemoveRequested(LobbyWidgetHandleElement handle)
        {
            if (!RequireLayout().Remove(handle.WidgetId))
                return;

            host.Apply();
            PlaceHandles();
            UpdateSaveButton();
        }

        private void MoveLifted(Vector2 panelPosition)
        {
            if (lifted == null)
                return;

            Vector2 local = RequireDragLayer().WorldToLocal(panelPosition - grabOffset);
            lifted.style.left = local.x;
            lifted.style.top = local.y;
            dragFrame.style.left = local.x - LobbyWidgetHandleElement.OUTSET;
            dragFrame.style.top = local.y - LobbyWidgetHandleElement.OUTSET;
        }

        // The modal is the target of a placed widget while the pointer is over it, as it is far smaller than most
        // widgets; an unplaced one has nothing to leave and the required widget never leaves, so neither targets the
        // modal. Otherwise the grid under the middle of the lifted widget is the target. The cell nearest to the top-left
        // corner of the widget must then hold the whole widget inside that grid, so a widget straddling two grids has no
        // valid cell
        private void UpdateTarget(Vector2 panelPosition)
        {
            Vector2Int span = draggedWidgetId.Span();
            Vector2 topLeft = panelPosition - grabOffset;
            bool overModal = draggedHandle != null && draggedWidgetId != LobbyLayout.REQUIRED_WIDGET && RequireModal().worldBound.Contains(panelPosition);
            LobbyGridElement? grid = overModal ? null : GridAt(topLeft + (LobbyGridElement.SpanToSize(span) * 0.5f));
            SetTargetModal(overModal);

            if (grid != targetGrid)
            {
                targetGrid?.SetHighlighted(false);
                targetGrid?.ClearDropHint();
                dropMarker.RemoveFromHierarchy();
                targetGrid = grid;

                if (grid != null)
                {
                    grid.SetHighlighted(true);
                    grid.Add(dropMarker);
                }
            }

            if (grid == null)
            {
                targetValid = false;
                ShowPushes();
                return;
            }

            targetCell = LobbyGridElement.NearestCell(grid.WorldToLocal(topLeft));
            targetValid = RequireLayout().TryResolvePush(draggedWidgetId, grid.Corner, targetCell, resolution);
            ShowPushes();

            Vector2 markerLocal = LobbyGridElement.CellToLocal(targetCell);
            dropMarker.style.left = markerLocal.x;
            dropMarker.style.top = markerLocal.y;
            dropMarker.EnableInClassList(USS_DROP_MARKER_VALID, targetValid);
            dropMarker.EnableInClassList(USS_DROP_MARKER_INVALID, !targetValid);
            grid.SetDropHint(targetCell, span, targetValid);
        }

        private LobbyGridElement? GridAt(Vector2 panelPosition)
        {
            for (var i = 0; i < CORNERS.Length; i++)
            {
                LobbyGridElement grid = host.Grid(CORNERS[i]);

                if (grid.worldBound.Contains(panelPosition))
                    return grid;
            }

            return null;
        }

        private void SetTargetModal(bool over)
        {
            if (over == targetModal)
                return;

            targetModal = over;
            RequireModal().EnableInClassList(USS_MODAL_TARGET, over);
        }

        // The widgets the resolution pushes stand at their pushed cells with their handles hidden, so only the widget
        // slides and no frame trails it; the ones the previous target pushed and this one does not go back to their
        // placement with their handle. The resolution is consumed, so the next target starts from none
        private void ShowPushes()
        {
            for (var i = 0; i < pushed.Count; i++)
                if (!IsPushed(pushed[i].WidgetId))
                    ShowAtPlacement(pushed[i].WidgetId);

            for (var i = 0; i < resolution.Count; i++)
            {
                host.ShowAt(resolution[i].WidgetId, resolution[i].Cell);
                handles[(int)resolution[i].WidgetId].SetPushed(true);
            }

            pushed.Clear();
            pushed.AddRange(resolution);
            resolution.Clear();
        }

        private bool IsPushed(LobbyWidgetId widgetId)
        {
            for (var i = 0; i < resolution.Count; i++)
                if (resolution[i].WidgetId == widgetId)
                    return true;

            return false;
        }

        private void ShowAtPlacement(LobbyWidgetId widgetId)
        {
            if (RequireLayout().TryGetPlacement(widgetId, out LobbyWidgetPlacement placement))
                host.ShowAt(widgetId, placement.Cell);

            handles[(int)widgetId].SetPushed(false);
        }

        private void ClearTarget()
        {
            targetGrid?.SetHighlighted(false);
            targetGrid?.ClearDropHint();
            targetGrid = null;
            targetValid = false;
            SetTargetModal(false);
            dropMarker.RemoveFromHierarchy();
            ShowPushes();
        }

        private static T Find<T>(VisualElement root, string elementName) where T: VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"The lobby hierarchy has no {elementName}");
    }
}