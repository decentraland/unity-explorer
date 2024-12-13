using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.StylizedSkybox.Scripts
{
    [CreateAssetMenu(menuName = "Create Stylized Skybox Settings", fileName = "StylizedSkyboxSettings", order = 0)]
    public class StylizedSkyboxSettingsAsset : ScriptableObject
    {
        public StylizedSkyboxControllerRef StylizedSkyboxPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        public event Action<int> TimeOfDayChanged;
        public event Action<TimeProgression> SpeedChanged;

        private int timeOfDay;

        public int TimeOfDay
        {
            get => timeOfDay;

            set
            {
                if (timeOfDay == value) return;

                Speed = TimeProgression.Paused;
                timeOfDay = value;
                TimeOfDayChanged?.Invoke(value);
            }
        }

        private TimeProgression speed = TimeProgression.Default;

        public TimeProgression Speed
        {
            get => speed;

            set
            {
                if (speed == value) return;

                speed = value;
                SpeedChanged?.Invoke(value);
            }
        }

        public enum TimeProgression
        {
            Paused = 0,
            Default = 1,
            Fast = 2,
            VeryFast = 3,
        }

        [Serializable]
        public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
        {
            public StylizedSkyboxControllerRef(string guid) : base(guid) { }
        }
    }
}
