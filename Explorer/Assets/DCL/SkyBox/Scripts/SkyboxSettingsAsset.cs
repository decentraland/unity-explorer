using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.SkyBox
{
    public enum TransitionMode
    {
        FORWARD,
        BACKWARD,
    }

    [CreateAssetMenu(menuName = "DCL/SO/Skybox Settings", fileName = "SkyboxSettings")]
    public class SkyboxSettingsAsset : ScriptableObject
    {
        private const float DEFAULT_SPEED = 1 * 60f; // 1 minute per second
        // We need to subtract 1 minute to make the slider range is between 00:00 and 23:59
        public const int TOTAL_MINUTES_IN_DAY = 1439; // 23:59 in minutes
        public const int SECONDS_IN_DAY = 86400;

        [SerializeField] private float speedMultiplier = DEFAULT_SPEED;
        [SerializeField] private float transitionSpeed = 1f;

        private float targetTransitionTimeOfDay;
        private bool canUIControl;
        private float timeOfDayNormalized;

        // For UI display synchronization
        public event Action<float> TimeOfDayChanged;

        public SkyboxRenderControllerRef SkyboxRenderControllerPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        public float initialTimeOfDay = 0.5f; // Midday
        public float refreshInterval = 5f;
        public bool ShouldUpdateSkybox { get; set; }
        public bool IsUIControlled { get; set; } // Set by UI global system
        public bool IsSDKControlled { get; set; } // Set by SDK component system
        public bool IsDayCycleEnabled { get; set; } = true;
        public TransitionMode TransitionMode { get; set; }
        public bool IsTransitioning { get; set; }

        public float SpeedMultiplier
        {
            get => speedMultiplier;

            set => speedMultiplier = value;
        }

        public float TransitionSpeed
        {
            get => transitionSpeed;

            set => transitionSpeed = value;
        }

        public bool CanUIControl
        {
            get => canUIControl;
            set => canUIControl = value;
        }

        public float TimeOfDayNormalized
        {
            get => timeOfDayNormalized;

            set
            {
                if (Mathf.Approximately(timeOfDayNormalized, value)) return;

                value = NormalizeTimeIfNeeded(value);

                timeOfDayNormalized = value;
                if(!IsUIControlled)
                    TimeOfDayChanged?.Invoke(timeOfDayNormalized);
            }
        }

        /// <summary>
        /// Target time of day for the transition, normalized between 0 and 1.
        /// Values greater than 1 are interpreted as seconds in a day and automatically normalized.
        /// </summary>
        public float TargetTransitionTimeOfDay
        {
            get => targetTransitionTimeOfDay;

            set
            {
                if (Mathf.Approximately(targetTransitionTimeOfDay, value)) return;
                targetTransitionTimeOfDay = NormalizeTimeIfNeeded(value);
            }
        }

        public void Reset()
        {
            TimeOfDayNormalized = initialTimeOfDay;
            IsDayCycleEnabled = true;
            IsTransitioning = false;
            ShouldUpdateSkybox =  true;
            SpeedMultiplier = DEFAULT_SPEED;
            TransitionMode = TransitionMode.FORWARD;
            CanUIControl = true;
            IsUIControlled = false;
            IsSDKControlled = false;
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
        public class SkyboxRenderControllerRef : ComponentReference<SkyboxRenderController>
        {
            public SkyboxRenderControllerRef(string guid) : base(guid) { }
        }
    }
}
