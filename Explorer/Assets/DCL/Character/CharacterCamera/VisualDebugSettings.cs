using System;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     Centralized static settings for visual debug controls.
    ///     Used to quickly toggle various rendering features from the
    ///     Performance debug panel.
    /// </summary>
    public static class VisualDebugSettings
    {
        // Minimap Camera
        public static event Action<bool>? OnMinimapCameraDisabledChanged;
        private static bool minimapCameraDisabled;
        public static bool MinimapCameraDisabled
        {
            get => minimapCameraDisabled;
            set
            {
                if (minimapCameraDisabled == value) return;
                minimapCameraDisabled = value;
                OnMinimapCameraDisabledChanged?.Invoke(value);
            }
        }

        // Current Scene - Renderer Visible
        public static event Action<bool>? OnSceneRendererVisibleChanged;
        private static bool sceneRendererVisible = true;
        public static bool SceneRendererVisible
        {
            get => sceneRendererVisible;
            set
            {
                if (sceneRendererVisible == value) return;
                sceneRendererVisible = value;
                OnSceneRendererVisibleChanged?.Invoke(value);
            }
        }

        // Current Scene - Backface Culling
        public static event Action<bool>? OnBackfaceCullingChanged;
        private static bool backfaceCullingEnabled;
        public static bool BackfaceCullingEnabled
        {
            get => backfaceCullingEnabled;
            set
            {
                if (backfaceCullingEnabled == value) return;
                backfaceCullingEnabled = value;
                OnBackfaceCullingChanged?.Invoke(value);
            }
        }

        // Current Scene - Shadow Limiter
        public static event Action<int>? OnShadowLimiterChanged;
        private static int shadowLimiterValue = -1;
        public static int ShadowLimiterValue
        {
            get => shadowLimiterValue;
            set
            {
                shadowLimiterValue = value;
                OnShadowLimiterChanged?.Invoke(value);
            }
        }

        // Landscape - All
        public static event Action<bool>? OnDisableLandscapeChanged;
        private static bool disableLandscape;
        public static bool DisableLandscape
        {
            get => disableLandscape;
            set
            {
                if (disableLandscape == value) return;
                disableLandscape = value;
                OnDisableLandscapeChanged?.Invoke(value);
            }
        }

        // Landscape - Ground
        public static event Action<bool>? OnDisableGroundChanged;
        private static bool disableGround;

        public static bool DisableGround
        {
            get => disableGround;

            set
            {
                if (disableGround == value) return;
                disableGround = value;
                OnDisableGroundChanged?.Invoke(value);
            }
        }

        // Landscape - Trees
        public static event Action<bool>? OnDisableTreesChanged;
        private static bool disableTrees;

        public static bool DisableTrees
        {
            get => disableTrees;

            set
            {
                if (disableTrees == value) return;
                disableTrees = value;
                OnDisableTreesChanged?.Invoke(value);
            }
        }

        // Landscape - Grass
        public static event Action<bool>? OnDisableGrassChanged;
        private static bool disableGrass;

        public static bool DisableGrass
        {
            get => disableGrass;

            set
            {
                if (disableGrass == value) return;
                disableGrass = value;
                OnDisableGrassChanged?.Invoke(value);
            }
        }

        // Landscape - Satellite
        public static event Action<bool>? OnDisableSatelliteChanged;
        private static bool disableSatellite;

        public static bool DisableSatellite
        {
            get => disableSatellite;

            set
            {
                if (disableSatellite == value) return;
                disableSatellite = value;
                OnDisableSatelliteChanged?.Invoke(value);
            }
        }

        // Roads - GPU Instancing Enable (uses GPUInstancingService.IsEnabled)
        public static event Action<bool>? OnRoadsEnabledChanged;
        private static bool roadsEnabled = true;
        public static bool RoadsEnabled
        {
            get => roadsEnabled;
            set
            {
                if (roadsEnabled == value) return;
                roadsEnabled = value;
                OnRoadsEnabledChanged?.Invoke(value);
            }
        }

        // Livekit Rooms - Activate/Deactivate both Island and Scene rooms
        public static event Action<bool>? OnLivekitRoomsActiveChanged;
        private static bool livekitRoomsActive = true;
        public static bool LivekitRoomsActive
        {
            get => livekitRoomsActive;
            set
            {
                if (livekitRoomsActive == value) return;
                livekitRoomsActive = value;
                OnLivekitRoomsActiveChanged?.Invoke(value);
            }
        }

        // LODs - Disable renderers of visible LODs
        public static event Action<bool>? OnLODRenderersDisabledChanged;
        private static bool lodRenderersDisabled;
        public static bool LODRenderersDisabled
        {
            get => lodRenderersDisabled;
            set
            {
                if (lodRenderersDisabled == value) return;
                lodRenderersDisabled = value;
                OnLODRenderersDisabledChanged?.Invoke(value);
            }
        }

        // Current Scene - Disable Shadows (directional light)
        public static event Action<bool>? OnShadowsDisabledChanged;
        private static bool shadowsDisabled;
        public static bool ShadowsDisabled
        {
            get => shadowsDisabled;
            set
            {
                if (shadowsDisabled == value) return;
                shadowsDisabled = value;
                OnShadowsDisabledChanged?.Invoke(value);
            }
        }
    }
}
