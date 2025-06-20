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
        SCENE_FIXED, // The scene manages the skybox time
        PLAYER_FIXED, // The player manages the skybox time
        FEATURE_FLAG, // The feature flag manages the skybox time
        GLOBAL, // The unmanaged global DCL skybox time
    }

    [CreateAssetMenu(menuName = "DCL/SO/Stylized Skybox Settings", fileName = "StylizedSkyboxSettings")]
    public class StylizedSkyboxSettingsAsset : ScriptableObject
    {
        public StylizedSkyboxControllerRef StylizedSkyboxPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        public event Action<float> TimeOfDayChanged;
        public event Action<bool> DayNightCycleEnabledChanged;
        public event Action<SkyboxTimeSource> SkyboxTimeSourceChanged;

        private SkyboxTimeSource skyboxSkyboxTimeSource = SkyboxTimeSource.GLOBAL;

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

        private float timeOfDayNormalized;

        public float TimeOfDayNormalized
        {
            get => timeOfDayNormalized;

            set
            {
                if (timeOfDayNormalized == value) return;

                timeOfDayNormalized = value;
                TimeOfDayChanged?.Invoke(value);
            }
        }

        private bool isDayNightCycleEnabled = true;

        public bool IsDayNightCycleEnabled
        {
            get => isDayNightCycleEnabled;

            set
            {
                if (isDayNightCycleEnabled == value) return;

                isDayNightCycleEnabled = value;
                DayNightCycleEnabledChanged?.Invoke(value);
            }
        }

        [Serializable]
        public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
        {
            public StylizedSkyboxControllerRef(string guid) : base(guid) { }
        }
    }
}
