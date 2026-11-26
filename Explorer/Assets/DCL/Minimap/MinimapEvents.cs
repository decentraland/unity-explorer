namespace DCL.Minimap
{
    public static class MinimapEvents
    {
        public enum Action
        {
            Expanded,
            Collapsed,
            MapClicked,
            FavoriteClicked,
            ContextMenuOpened,
            SkyboxWidgetToggled,
            SkyboxTimeChanged,
            SkyboxDayCycleToggled,
        }

        /// <summary>
        ///     Published on the analytics event bus whenever the user interacts with the minimap.
        ///     <see cref="State" /> carries the new boolean state for the actions that have one,
        ///     <see cref="TimeOfDay" /> the normalized time of day for <see cref="Action.SkyboxTimeChanged" />.
        /// </summary>
        public readonly struct Interaction
        {
            public readonly Action Action;
            public readonly bool? State;
            public readonly float? TimeOfDay;

            public Interaction(Action action, bool? state = null, float? timeOfDay = null)
            {
                Action = action;
                State = state;
                TimeOfDay = timeOfDay;
            }
        }
    }
}
