using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     Keeps the widget sections where the layout places them: a placed section hangs from the grid of its corner, at the
    ///     position of its cell and as wide as its span; an unplaced one hangs from a hidden container, whatever its own
    ///     display is. The sections keep hiding themselves while empty.
    /// </summary>
    public class LobbyWidgetHost
    {
        private const string TOP_LEFT_GRID_NAME = "TopLeftGrid";
        private const string TOP_RIGHT_GRID_NAME = "TopRightGrid";
        private const string BOTTOM_LEFT_GRID_NAME = "BottomLeftGrid";
        private const string BOTTOM_RIGHT_GRID_NAME = "BottomRightGrid";
        private const string UNPLACED_WIDGETS_NAME = "UnplacedWidgets";
        private const string WELCOME_NAME = "Welcome";
        private const string EVENTS_NAME = "Events";
        private const string FRIENDS_NAME = "Friends";
        private const string RECENT_PLACES_NAME = "RecentPlaces";
        private const string FEATURED_PLACES_NAME = "FeaturedPlaces";

        private static readonly LobbyWidgetId[] WIDGETS = (LobbyWidgetId[])Enum.GetValues(typeof(LobbyWidgetId));

        private readonly LobbyGridElement[] grids = new LobbyGridElement[LobbyLayout.CORNER_COUNT];
        private readonly VisualElement[] sections = new VisualElement[WIDGETS.Length];

        private VisualElement? unplacedWidgets;

        /// <summary>The layout in force, built from the grids of the hierarchy. Null until shown.</summary>
        public LobbyLayout? Layout { get; private set; }

        /// <summary>
        ///     Finds the grids and the sections under the root, loads the stored layout and lays the sections out.
        /// </summary>
        public void Show(VisualElement root)
        {
            grids[(int)LobbyGridCorner.TopLeft] = Find<LobbyGridElement>(root, TOP_LEFT_GRID_NAME);
            grids[(int)LobbyGridCorner.TopRight] = Find<LobbyGridElement>(root, TOP_RIGHT_GRID_NAME);
            grids[(int)LobbyGridCorner.BottomLeft] = Find<LobbyGridElement>(root, BOTTOM_LEFT_GRID_NAME);
            grids[(int)LobbyGridCorner.BottomRight] = Find<LobbyGridElement>(root, BOTTOM_RIGHT_GRID_NAME);
            unplacedWidgets = Find<VisualElement>(root, UNPLACED_WIDGETS_NAME);

            sections[(int)LobbyWidgetId.Landing4x3] = Find<VisualElement>(root, WELCOME_NAME);
            sections[(int)LobbyWidgetId.Events3x4] = Find<VisualElement>(root, EVENTS_NAME);
            sections[(int)LobbyWidgetId.FriendsOnline4x2] = Find<VisualElement>(root, FRIENDS_NAME);
            sections[(int)LobbyWidgetId.JumpBackIn6x2] = Find<VisualElement>(root, RECENT_PLACES_NAME);
            sections[(int)LobbyWidgetId.RecommendedPlaces6x2] = Find<VisualElement>(root, FEATURED_PLACES_NAME);

            Layout = new LobbyLayout(
                grids[(int)LobbyGridCorner.TopLeft].Size,
                grids[(int)LobbyGridCorner.TopRight].Size,
                grids[(int)LobbyGridCorner.BottomLeft].Size,
                grids[(int)LobbyGridCorner.BottomRight].Size);

            LobbyLayoutStorage.Load(Layout);
            Apply();
        }

        /// <summary>Moves every section to where <see cref="Layout" /> has it now.</summary>
        public void Apply()
        {
            if (Layout == null || unplacedWidgets == null)
                throw new InvalidOperationException("The widget host lays out the sections only after it is shown");

            for (var i = 0; i < WIDGETS.Length; i++)
            {
                LobbyWidgetId widgetId = WIDGETS[i];
                VisualElement section = sections[(int)widgetId];

                if (!Layout.TryGetPlacement(widgetId, out LobbyWidgetPlacement placement))
                {
                    if (section.parent != unplacedWidgets)
                        unplacedWidgets.Add(section);

                    continue;
                }

                LobbyGridElement grid = grids[(int)placement.Corner];

                if (section.parent != grid)
                    grid.Add(section);

                Vector2 local = LobbyGridElement.CellToLocal(placement.Cell);
                section.style.left = local.x;
                section.style.top = local.y;
                section.style.width = LobbyGridElement.SpanToSize(widgetId.Span()).x;
            }
        }

        private static T Find<T>(VisualElement root, string elementName) where T: VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"The lobby hierarchy has no {elementName}");
    }
}
