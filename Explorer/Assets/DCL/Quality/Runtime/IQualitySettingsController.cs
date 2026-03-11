using System;
using UnityEngine;

namespace DCL.Quality.Runtime
{
    public interface IQualitySettingsController : IDisposable
    {
        event Action<QualityPresetLevel> OnPresetChanged;

        // Preset
        QualityPresetLevel CurrentPreset { get; }

        // Display
        int FpsLimit { get; }
        bool VSync { get; }
        float ResolutionScale { get; }
        int ResolutionWidth { get; }
        int ResolutionHeight { get; }
        RefreshRate ResolutionRefreshRate { get; }
        FullScreenMode WindowMode { get; }

        // Post Processing
        MsaaLevel Msaa { get; }
        bool Hdr { get; }
        bool Bloom { get; }
        bool AvatarOutline { get; }

        // Landscape & Foliage
        int SceneDistance { get; }
        float LandscapeDistance { get; }

        // Sun
        bool SunShadows { get; }

        // Scene lighting
        bool SceneLights { get; }
        bool SceneLightShadows { get; }
        int MaxSceneLights { get; }

        // General Shadows
        ShadowQualityLevel SceneShadowQuality { get; }
        int ShadowDistance { get; }

        /// <summary>
        ///     Applies a full preset, resetting all individual overrides
        /// </summary>
        void SetPreset(QualityPresetLevel level);

        /// <summary>
        ///     Re-applies all current settings to the render pipeline
        /// </summary>
        void ApplyAllSettings();

        // Display
        void SetFpsLimit(int fps);
        void SetVSync(bool enabled);
        void SetResolutionScale(float scale);
        void SetResolution(int width, int height, RefreshRate refreshRate);
        void SetWindowMode(int index);

        // Post Processing
        void SetMsaa(MsaaLevel level);
        void SetHdr(bool enabled);
        void SetBloom(bool enabled);
        void SetAvatarOutline(bool enabled);

        // Landscape & Foliage
        void SetSceneDistance(int distance);
        void SetLandscapeDistance(float distance);

        // Sun
        void SetSunShadows(bool enabled);

        // Scene lighting
        void SetSceneLights(bool enabled);
        void SetMaxSceneLights(int max);
        void SetSceneLightShadows(bool enabled);

        // General Shadows
        void SetShadowQuality(ShadowQualityLevel level);
        void SetShadowDistance(int distance);
    }
}
