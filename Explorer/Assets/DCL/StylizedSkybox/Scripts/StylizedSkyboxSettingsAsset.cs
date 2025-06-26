using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.StylizedSkybox.Scripts
{
    // These are the possible time sources for the skybox ordered by priority
    // Note: More time sources will be added in the future
    public enum SkyboxTimeSource
    {
        SDK_SKYBOX_COMPONENT_FIXED, // The SDK skybox time component manages the skybox time
        SCENE_FIXED, // The scene manages the skybox time
        PLAYER_FIXED, // The player manages the skybox time
        FEATURE_FLAG, // The feature flag manages the skybox time
        GLOBAL, // The unmanaged global DCL skybox time
    }

    [CreateAssetMenu(menuName = "DCL/SO/Stylized Skybox Settings", fileName = "StylizedSkyboxSettings")]
    public class StylizedSkyboxSettingsAsset : ScriptableObject
    {
        public const int SECONDS_IN_DAY = 86400;
        public const float DEFAULT_TIME = 0.5f; // Midday
        // We need to subtract 1 minute to make the slider range is between 00:00 and 23:59
        public const int TOTAL_MINUTES_IN_DAY = 1439; // 23:59 in minutes

        public StylizedSkyboxControllerRef StylizedSkyboxPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        public event Action<float> TimeOfDayChanged;
        public event Action<SkyboxTimeSource> SkyboxTimeSourceChanged;
        public event Action<bool> DayCycleEnabledChanged;

        private bool isDayCycleEnabled = true;
        private SkyboxTimeSource skyboxSkyboxTimeSource = SkyboxTimeSource.GLOBAL;
        private float timeOfDayNormalized;

        public bool IsDayCycleEnabled {
            get => isDayCycleEnabled;

            set
            {
                if(isDayCycleEnabled == value) return;

                isDayCycleEnabled = value;
                DayCycleEnabledChanged?.Invoke(value);
            }
        }

        public SkyboxTimeSource SkyboxTimeSource
        {
            get => skyboxSkyboxTimeSource;

            set
            {
                if(skyboxSkyboxTimeSource == value) return;

                skyboxSkyboxTimeSource = value;
                SkyboxTimeSourceChanged?.Invoke(value);
            }
        }

        public float TimeOfDayNormalized
        {
            get => timeOfDayNormalized;

            set
            {
                if (Mathf.Approximately(timeOfDayNormalized, value)) return;

                value = NormalizeTimeIfNeeded(value);

                timeOfDayNormalized = value;
                TimeOfDayChanged?.Invoke(value);
            }
        }

        private float NormalizeTimeIfNeeded(float time)
        {
            if (time < 0)
                return 0;

            if (time <= 1)
                return time;

            time %= SECONDS_IN_DAY;
            return time / SECONDS_IN_DAY;
        }

        [Serializable]
        public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
        {
            public StylizedSkyboxControllerRef(string guid) : base(guid) { }
        }

        public void Reset()
        {
            SkyboxTimeSource = SkyboxTimeSource.GLOBAL;
            TimeOfDayNormalized = DEFAULT_TIME;
            IsDayCycleEnabled = true;
        }
    }
}
