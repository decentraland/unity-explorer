namespace DCL.Minimap
{
    /// <summary>
    ///     Published on the event bus when the user interacts with the minimap.
    /// </summary>
    public static class MinimapEvents
    {
        public readonly struct Expanded { }

        public readonly struct Collapsed { }

        public readonly struct MapClicked { }

        public readonly struct ContextMenuOpened { }

        public readonly struct FavoriteClicked
        {
            public readonly bool IsFavorite;

            public FavoriteClicked(bool isFavorite)
            {
                IsFavorite = isFavorite;
            }
        }

        public readonly struct SkyboxWidgetToggled
        {
            public readonly bool IsOpen;

            public SkyboxWidgetToggled(bool isOpen)
            {
                IsOpen = isOpen;
            }
        }

        /// <summary>
        ///     Published on every step of the time slider.
        /// </summary>
        public readonly struct SkyboxTimeChanged
        {
            public readonly float TimeOfDayNormalized;

            public SkyboxTimeChanged(float timeOfDayNormalized)
            {
                TimeOfDayNormalized = timeOfDayNormalized;
            }
        }

        public readonly struct SkyboxDayCycleToggled
        {
            public readonly bool IsEnabled;

            public SkyboxDayCycleToggled(bool isEnabled)
            {
                IsEnabled = isEnabled;
            }
        }
    }
}
