using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Edit mode of the lobby layout: every placed widget gets a handle over its cells that can be dragged onto other
    ///     cells, and every unplaced widget a row in the widget modal over the avatar that can be dragged onto a grid. The
    ///     widget itself, or the preview of an unplaced one at full size, follows the pointer on the drag layer, its cells
    ///     left clear behind it. A drop takes only when the whole widget fits inside one grid; the widgets in its way make
    ///     room by moving off the side it covers, and they slide to where they would go, without their handles, while the
    ///     drag hovers over them. A release with the pointer over the modal takes the widget off its grid, as does
    ///     the button in the corner of a placed handle. Any other release leaves every widget where it was. Every drop
    ///     that takes is stored at once.
    /// </summary>
    public class LobbyLayoutEditor
    {
        private const string DRAG_LAYER_NAME = "LayoutDragLayer";
        private const string MODAL_NAME = "WidgetModal";
        private const string MODAL_ITEMS_NAME = "WidgetModalItems";
        private const string USS_EDITING = "lobby--editing";
        private const string USS_WIDGET_LIFTED = "lobby__widget--lifted";
        private const string USS_GRID_SHIELD = "lobby-grid-shield";
        private const string USS_DROP_MARKER = "lobby-drop-marker";
        private const string USS_DROP_MARKER_VALID = USS_DROP_MARKER + "--valid";
        private const string USS_DROP_MARKER_INVALID = USS_DROP_MARKER + "--invalid";
        private const string USS_MODAL_TARGET = "lobby__widget-modal--target";

        private static readonly LobbyWidgetId[] WIDGETS = (LobbyWidgetId[])Enum.GetValues(typeof(LobbyWidgetId));
        private static readonly LobbyGridCorner[] CORNERS = (LobbyGridCorner[])Enum.GetValues(typeof(LobbyGridCorner));

        private readonly LobbyWidgetHost host;
        private readonly LobbyWidgetHandleElement[] handles = new LobbyWidgetHandleElement[WIDGETS.Length];

        /// <summary>One per grid, between its sections and its handles, so no widget hears the pointer while edited.</summary>
        private readonly VisualElement[] shields = new VisualElement[CORNERS.Length];
        private readonly VisualElement dropMarker;

        /// <summary>Where the widgets in the way of the drop stand while the drag hovers over them.</summary>
        private readonly List<LobbyWidgetPlacement> pushed = new ();
        private readonly List<LobbyWidgetPlacement> resolution = new ();

        private VisualElement? root;
        private VisualElement? dragLayer;
        private VisualElement? modal;
        private VisualElement? modalItems;
        private LobbyWidgetHandleElement? dragged;

        /// <summary>The section or the preview of the dragged widget while it follows the pointer on the drag layer.</summary>
        private VisualElement? lifted;

        /// <summary>Panel pixels from the top-left corner of the dragged handle to the pointer.</summary>
        private Vector2 grabOffset;
        private LobbyGridElement? targetGrid;
        private Vector2Int targetCell;
        private bool targetValid;
        private bool targetModal;

        public bool IsEditing => root != null;

        private LobbyLayout Layout => host.Layout ?? throw new InvalidOperationException("The layout can be edited only once the widget host is shown");

        private VisualElement DragLayer => dragLayer ?? throw new InvalidOperationException("The layout can be dragged only while the editor is shown");

        private VisualElement Modal => modal ?? throw new InvalidOperationException("The widget modal exists only while the editor is shown");

        private VisualElement ModalItems => modalItems ?? throw new InvalidOperationException("The widget modal exists only while the editor is shown");

        public LobbyLayoutEditor(LobbyWidgetHost host)
        {
            this.host = host;

            for (var i = 0; i < WIDGETS.Length; i++)
            {
                LobbyWidgetId widgetId = WIDGETS[i];

                handles[(int)widgetId] = new LobbyWidgetHandleElement(widgetId, Title(widgetId))
                {
                    name = widgetId.ToString(),
                    DragStarted = OnDragStarted,
                    Dragged = OnDragged,
                    DragEnded = OnDragEnded,
                    RemoveRequested = OnRemoveRequested,
                };
            }

            for (var i = 0; i < CORNERS.Length; i++)
            {
                shields[i] = new VisualElement { name = "GridShield" };
                shields[i].AddToClassList(USS_GRID_SHIELD);
            }

            dropMarker = new VisualElement { name = "DropMarker", pickingMode = PickingMode.Ignore };
            dropMarker.AddToClassList(USS_DROP_MARKER);
        }

        /// <summary>
        ///     Enters edit mode on the hierarchy the widget host was last shown with: the grids show their anchor dots, a
        ///     handle covers every placed widget and the modal lists every unplaced one.
        /// </summary>
        public void Show(VisualElement newRoot)
        {
            if (root != null)
                Hide();

            root = newRoot;
            dragLayer = Find(newRoot, DRAG_LAYER_NAME);
            modal = Find(newRoot, MODAL_NAME);
            modalItems = Find(newRoot, MODAL_ITEMS_NAME);
            newRoot.AddToClassList(USS_EDITING);

            for (var i = 0; i < CORNERS.Length; i++)
                host.Grid(CORNERS[i]).SetEditing(true);

            PlaceHandles();
        }

        /// <summary>Leaves edit mode; a drag in progress is dropped where the widget came from.</summary>
        public void Hide()
        {
            if (root == null)
                return;

            if (dragged != null)
            {
                ClearTarget();
                dragged.Cancel();
                dragged = null;
            }

            for (var i = 0; i < handles.Length; i++)
                handles[i].RemoveFromHierarchy();

            for (var i = 0; i < shields.Length; i++)
                shields[i].RemoveFromHierarchy();

            dropMarker.RemoveFromHierarchy();

            for (var i = 0; i < CORNERS.Length; i++)
            {
                LobbyGridElement grid = host.Grid(CORNERS[i]);
                grid.SetEditing(false);
                grid.SetHighlighted(false);
            }

            root.RemoveFromClassList(USS_EDITING);
            root = null;
            dragLayer = null;
            modal = null;
            modalItems = null;
        }

        // The shield of each grid goes in after the sections so it takes the pointer from whatever they hang outside
        // their cells, and the placed handles after it so they draw over everything and take the pointer first; the
        // unplaced handles line up in the modal in the order of the widget ids
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

                if (!Layout.TryGetPlacement(handle.WidgetId, out LobbyWidgetPlacement placement))
                {
                    handle.SetShelved(true);
                    ModalItems.Add(handle);
                    continue;
                }

                handle.SetShelved(false);
                Move(handle, placement.Cell);
                host.Grid(placement.Corner).Add(handle);
            }
        }

        // The section of a placed widget is lifted onto the drag layer so it draws over every grid, and its slide
        // transition is switched off so it keeps up with the pointer; the reparent itself runs no transition. The preview
        // of a shelved widget is lifted instead at full size, hanging from its middle as its row is far smaller than the
        // widget, while a placed one keeps the pointer where it was grabbed
        private void OnDragStarted(LobbyWidgetHandleElement handle, Vector2 panelPosition)
        {
            dragged = handle;
            Vector2 size = LobbyGridElement.SpanToSize(handle.WidgetId.Span());
            handle.SetDragging(true);

            dropMarker.style.width = size.x;
            dropMarker.style.height = size.y;

            if (handle.IsShelved)
            {
                grabOffset = size * 0.5f;
                handle.Preview.SetScale(1f);
                lifted = handle.Preview;
            }
            else
            {
                grabOffset = panelPosition - handle.worldBound.position;
                lifted = host.Section(handle.WidgetId);
            }

            lifted.AddToClassList(USS_WIDGET_LIFTED);
            DragLayer.Add(lifted);

            MoveLifted(panelPosition);
            UpdateTarget(handle, panelPosition);
        }

        private void OnDragged(LobbyWidgetHandleElement handle, Vector2 panelPosition, Vector2 _)
        {
            MoveLifted(panelPosition);
            UpdateTarget(handle, panelPosition);
        }

        // The host lands the lifted section where the drop left it: on its grid at the cell the drop took or the one it
        // came from, or off every grid; a lifted preview goes back into its row or away with the placed handle
        private void OnDragEnded(LobbyWidgetHandleElement handle, Vector2 _)
        {
            bool changed = targetModal
                ? Layout.Remove(handle.WidgetId)
                : targetValid && targetGrid != null && Layout.TryPlacePushing(handle.WidgetId, targetGrid.Corner, targetCell);

            if (changed)
                LobbyLayoutStorage.Save(Layout);

            ClearTarget();
            lifted?.RemoveFromClassList(USS_WIDGET_LIFTED);
            lifted = null;
            host.Apply();
            handle.SetDragging(false);
            dragged = null;
            PlaceHandles();
        }

        // The button in the corner of a placed handle does what a drop on the modal does, without the drag
        private void OnRemoveRequested(LobbyWidgetHandleElement handle)
        {
            if (!Layout.Remove(handle.WidgetId))
                return;

            LobbyLayoutStorage.Save(Layout);
            host.Apply();
            PlaceHandles();
        }

        private void MoveLifted(Vector2 panelPosition)
        {
            if (lifted == null)
                return;

            Vector2 local = DragLayer.WorldToLocal(panelPosition - grabOffset);
            lifted.style.left = local.x;
            lifted.style.top = local.y;
        }

        // The modal is the target while the pointer is over it, as it is far smaller than most widgets; otherwise the grid
        // under the middle of the lifted widget is. The cell nearest to the top-left corner of the widget must then hold
        // the whole widget inside that grid, so a widget straddling two grids has no valid cell
        private void UpdateTarget(LobbyWidgetHandleElement handle, Vector2 panelPosition)
        {
            Vector2Int span = handle.WidgetId.Span();
            Vector2 topLeft = panelPosition - grabOffset;
            bool overModal = Modal.worldBound.Contains(panelPosition);
            LobbyGridElement? grid = overModal ? null : GridAt(topLeft + (LobbyGridElement.SpanToSize(span) * 0.5f));
            SetTargetModal(overModal);

            if (grid != targetGrid)
            {
                targetGrid?.SetHighlighted(false);
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
            targetValid = Layout.TryResolvePush(handle.WidgetId, grid.Corner, targetCell, resolution);
            ShowPushes();

            Vector2 markerLocal = LobbyGridElement.CellToLocal(targetCell);
            dropMarker.style.left = markerLocal.x;
            dropMarker.style.top = markerLocal.y;
            dropMarker.EnableInClassList(USS_DROP_MARKER_VALID, targetValid);
            dropMarker.EnableInClassList(USS_DROP_MARKER_INVALID, !targetValid);
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
            Modal.EnableInClassList(USS_MODAL_TARGET, over);
        }

        // The widgets the resolution pushes stand at their pushed cells with their handles hidden, so only the widget
        // slides and no card trails it; the ones the previous target pushed and this one does not go back to their
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
            if (Layout.TryGetPlacement(widgetId, out LobbyWidgetPlacement placement))
                host.ShowAt(widgetId, placement.Cell);

            handles[(int)widgetId].SetPushed(false);
        }

        private void ClearTarget()
        {
            targetGrid?.SetHighlighted(false);
            targetGrid = null;
            targetValid = false;
            SetTargetModal(false);
            dropMarker.RemoveFromHierarchy();
            ShowPushes();
        }

        private static void Move(VisualElement element, Vector2Int cell)
        {
            Vector2 local = LobbyGridElement.CellToLocal(cell);
            element.style.left = local.x;
            element.style.top = local.y;
        }

        private static VisualElement Find(VisualElement root, string elementName) =>
            root.Q(elementName) ?? throw new InvalidOperationException($"The lobby hierarchy has no {elementName}");

        private static string Title(LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.Landing4x3 => "Welcome",
                LobbyWidgetId.LiveEvents3x3 => "Live Events",
                LobbyWidgetId.UpcomingEvents3x2 => "Upcoming Events",
                LobbyWidgetId.FriendsOnline4x2 => "Friends",
                LobbyWidgetId.JumpBackIn6x2 => "Jump Back In",
                LobbyWidgetId.JumpBackIn4x2 => "Jump Back In",
                LobbyWidgetId.RecommendedPlaces6x2 => "Recommended Places",
                LobbyWidgetId.RecommendedPlaces4x2 => "Recommended Places",
                _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, null),
            };
    }
}
