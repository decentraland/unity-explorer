using System;

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

        // Post Processing
        bool Msaa { get; }
        bool Hdr { get; }
        bool Bloom { get; }
        bool AvatarOutline { get; }

        // Landscape & Foliage
        int SceneDistance { get; }
        float LandscapeDistance { get; }
        GrassPreset Grass { get; }

        // Lighting & Shadows
        bool SceneLights { get; }
        bool SceneLightShadows { get; }
        int MaxSceneLights { get; }
        ShadowQualityLevel ShadowQuality { get; }
        ShadowDistanceLevel ShadowDistance { get; }

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

        // Post Processing
        void SetMsaa(bool enabled);
        void SetHdr(bool enabled);
        void SetBloom(bool enabled);
        void SetAvatarOutline(bool enabled);

        // Landscape & Foliage
        void SetSceneDistance(int distance);
        void SetLandscapeDistance(float distance);
        void SetGrassPreset(GrassPreset preset);

        // Lighting & Shadows
        void SetSceneLights(bool enabled);
        void SetSceneLightShadows(bool enabled);
        void SetMaxSceneLights(int max);
        void SetShadowQuality(ShadowQualityLevel level);
        void SetShadowDistance(ShadowDistanceLevel level);
    }
}
