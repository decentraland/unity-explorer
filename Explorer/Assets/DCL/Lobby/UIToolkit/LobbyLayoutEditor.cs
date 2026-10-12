using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Edit mode of the lobby layout: every placed widget gets a handle over its cells that can be dragged onto other
    ///     cells. The widget itself follows the pointer on the drag layer while its dimmed handle marks where it came from.
    ///     A drop takes only when the whole widget fits inside one grid; the widgets in its way make room by moving off the
    ///     side it covers, and they show where they would go while the drag hovers over them. Any other release leaves
    ///     every widget where it was. Every drop that takes is stored at once.
    /// </summary>
    public class LobbyLayoutEditor
    {
        private const string DRAG_LAYER_NAME = "LayoutDragLayer";
        private const string USS_EDITING = "lobby--editing";
        private const string USS_WIDGET_LIFTED = "lobby__widget--lifted";
        private const string USS_DROP_MARKER = "lobby-drop-marker";
        private const string USS_DROP_MARKER_VALID = USS_DROP_MARKER + "--valid";
        private const string USS_DROP_MARKER_INVALID = USS_DROP_MARKER + "--invalid";

        private static readonly LobbyWidgetId[] WIDGETS = (LobbyWidgetId[])Enum.GetValues(typeof(LobbyWidgetId));
        private static readonly LobbyGridCorner[] CORNERS = (LobbyGridCorner[])Enum.GetValues(typeof(LobbyGridCorner));

        private readonly LobbyWidgetHost host;
        private readonly LobbyWidgetHandleElement[] handles = new LobbyWidgetHandleElement[WIDGETS.Length];
        private readonly VisualElement dropMarker;

        /// <summary>Where the widgets in the way of the drop stand while the drag hovers over them.</summary>
        private readonly List<LobbyWidgetPlacement> pushed = new ();
        private readonly List<LobbyWidgetPlacement> resolution = new ();

        private VisualElement? root;
        private VisualElement? dragLayer;
        private LobbyWidgetHandleElement? dragged;

        /// <summary>The section of the dragged widget while it follows the pointer on the drag layer.</summary>
        private VisualElement? lifted;

        /// <summary>Panel pixels from the top-left corner of the dragged handle to the pointer.</summary>
        private Vector2 grabOffset;
        private LobbyGridElement? targetGrid;
        private Vector2Int targetCell;
        private bool targetValid;

        public bool IsEditing => root != null;

        private LobbyLayout Layout => host.Layout ?? throw new InvalidOperationException("The layout can be edited only once the widget host is shown");

        private VisualElement DragLayer => dragLayer ?? throw new InvalidOperationException("The layout can be dragged only while the editor is shown");

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
                };
            }

            dropMarker = new VisualElement { name = "DropMarker", pickingMode = PickingMode.Ignore };
            dropMarker.AddToClassList(USS_DROP_MARKER);
        }

        /// <summary>
        ///     Enters edit mode on the hierarchy the widget host was last shown with: the grids show their anchor dots and a
        ///     handle covers every placed widget.
        /// </summary>
        public void Show(VisualElement newRoot)
        {
            if (root != null)
                Hide();

            root = newRoot;
            dragLayer = newRoot.Q(DRAG_LAYER_NAME) ?? throw new InvalidOperationException($"The lobby hierarchy has no {DRAG_LAYER_NAME}");
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
        }

        // The handles go in after the sections so they draw over them and take the pointer first
        private void PlaceHandles()
        {
            for (var i = 0; i < handles.Length; i++)
            {
                LobbyWidgetHandleElement handle = handles[i];
                handle.RemoveFromHierarchy();

                if (!Layout.TryGetPlacement(handle.WidgetId, out LobbyWidgetPlacement placement))
                    continue;

                Move(handle, placement.Cell);
                host.Grid(placement.Corner).Add(handle);
            }
        }

        // The section is lifted onto the drag layer so it draws over every grid, and its slide transition is switched off
        // so it keeps up with the pointer; the reparent itself runs no transition
        private void OnDragStarted(LobbyWidgetHandleElement handle, Vector2 panelPosition)
        {
            dragged = handle;
            grabOffset = panelPosition - handle.worldBound.position;
            handle.SetDragging(true);

            Vector2 size = LobbyGridElement.SpanToSize(handle.WidgetId.Span());
            dropMarker.style.width = size.x;
            dropMarker.style.height = size.y;

            lifted = host.Section(handle.WidgetId);
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

        // The host lands the lifted section back on its grid, at the cell the drop took or the one it came from
        private void OnDragEnded(LobbyWidgetHandleElement handle, Vector2 _)
        {
            if (targetValid && targetGrid != null && Layout.TryPlacePushing(handle.WidgetId, targetGrid.Corner, targetCell))
                LobbyLayoutStorage.Save(Layout);

            ClearTarget();
            lifted?.RemoveFromClassList(USS_WIDGET_LIFTED);
            lifted = null;
            host.Apply();
            handle.SetDragging(false);
            dragged = null;
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

        // The grid under the middle of the lifted widget is the target; the cell nearest to its top-left corner must then
        // hold the whole widget inside that grid, so a widget straddling two grids has no valid cell
        private void UpdateTarget(LobbyWidgetHandleElement handle, Vector2 panelPosition)
        {
            Vector2Int span = handle.WidgetId.Span();
            Vector2 topLeft = panelPosition - grabOffset;
            LobbyGridElement? grid = GridAt(topLeft + (LobbyGridElement.SpanToSize(span) * 0.5f));

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

        // The widgets the resolution pushes stand at their pushed cells; the ones the previous target pushed and this one
        // does not go back to their placement. The resolution is consumed, so the next target starts from none
        private void ShowPushes()
        {
            for (var i = 0; i < pushed.Count; i++)
                if (!IsPushed(pushed[i].WidgetId))
                    ShowAtPlacement(pushed[i].WidgetId);

            for (var i = 0; i < resolution.Count; i++)
                Show(resolution[i].WidgetId, resolution[i].Cell);

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
                Show(widgetId, placement.Cell);
        }

        private void Show(LobbyWidgetId widgetId, Vector2Int cell)
        {
            host.ShowAt(widgetId, cell);
            Move(handles[(int)widgetId], cell);
        }

        private void ClearTarget()
        {
            targetGrid?.SetHighlighted(false);
            targetGrid = null;
            targetValid = false;
            dropMarker.RemoveFromHierarchy();
            ShowPushes();
        }

        private static void Move(VisualElement element, Vector2Int cell)
        {
            Vector2 local = LobbyGridElement.CellToLocal(cell);
            element.style.left = local.x;
            element.style.top = local.y;
        }

        private static string Title(LobbyWidgetId widgetId) =>
            widgetId switch
            {
                LobbyWidgetId.Landing4x3 => "Welcome",
                LobbyWidgetId.LiveEvents3x3 => "Live Events",
                LobbyWidgetId.UpcomingEvents3x2 => "Upcoming Events",
                LobbyWidgetId.FriendsOnline4x2 => "Friends",
                LobbyWidgetId.JumpBackIn6x2 => "Jump Back In",
                LobbyWidgetId.RecommendedPlaces6x2 => "Recommended Places",
                _ => throw new ArgumentOutOfRangeException(nameof(widgetId), widgetId, null),
            };
    }
}
