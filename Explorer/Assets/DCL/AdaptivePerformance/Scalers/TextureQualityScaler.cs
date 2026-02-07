using UnityEngine;
using UnityEngine.AdaptivePerformance;

namespace DCL.AdaptivePerformance.Scalers
{
    public class TextureQualityScaler : AdaptivePerformanceScaler
    {
        // Define the default settings for the scaler.
        private AdaptivePerformanceScalerSettingsBase m_AdaptiveTextureQualityScaler = new AdaptivePerformanceScalerSettingsBase
        {
            // This name must exactly match the class name.
            name = "TextureQualityScaler",
            enabled = false,
            scale = 1.0f,
            visualImpact = ScalerVisualImpact.High,
            target = ScalerTarget.GPU,
            minBound = 0,
            maxBound = 4,
            maxLevel = 4
        };

        private int defaultTextureLimit;

        protected override void Awake()
        {
            base.Awake();

            // Apply the default settings defined above.
            ApplyDefaultSetting(m_AdaptiveTextureQualityScaler);
        }

        protected override void OnEnabled()
        {
            defaultTextureLimit = QualitySettings.globalTextureMipmapLimit;
        }

        protected override void OnDisabled()
        {
            QualitySettings.globalTextureMipmapLimit = defaultTextureLimit;
        }

        protected override void OnLevel()
        {
            if (ScaleChanged())
            {
                QualitySettings.globalTextureMipmapLimit = (int)MaxBound - ((int)(MaxBound * Scale));
            }
        }
    }
}
