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
        public const float INITIAL_TIME_OF_DAY = 0.5f; // Midday
        public const float REFRESH_INTERVAL = 5f;

        [SerializeField] private float speedMultiplier = DEFAULT_SPEED;
        [SerializeField] private float transitionSpeed = 1f;

        private float timeOfDayNormalized;

        public event Action<float>? TimeOfDayChanged;

        public SkyboxRenderControllerRef SkyboxRenderControllerPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        public bool ShouldUpdateSkybox { get; set; }
        public bool IsUIControlled { get; set; } // Set by UI global system
        public bool IsSDKControlled { get; set; } // Set by SDK component system
        public bool IsDayCycleEnabled { get; set; }
        public TransitionMode TransitionMode { get; set; }

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

        public bool CanUIControl { get; set; } = true;

        public float TimeOfDayNormalized
        {
            get => timeOfDayNormalized;

            set
            {
                if (Mathf.Approximately(timeOfDayNormalized, value)) return;

                timeOfDayNormalized = value;

                if (!IsUIControlled)
                    TimeOfDayChanged?.Invoke(timeOfDayNormalized);
            }
        }

        public float TargetTimeOfDayNormalized { get; set; }

        public void Reset()
        {
            timeOfDayNormalized = INITIAL_TIME_OF_DAY;
            TargetTimeOfDayNormalized = INITIAL_TIME_OF_DAY;
            IsDayCycleEnabled = true;
            ShouldUpdateSkybox = true;
            TransitionMode = TransitionMode.FORWARD;
            CanUIControl = true;
            IsUIControlled = false;
            IsSDKControlled = false;
        }

        public static float NormalizeTime(float time)
        {
            if (time < 0)
                return 0;

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
