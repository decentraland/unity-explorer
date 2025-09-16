﻿using DCL.AssetsProvision;
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
        private const int SECONDS_IN_DAY = 86400;

        // We need to subtract 1 minute to make the slider range is between 00:00 and 23:59
        public const int TOTAL_MINUTES_IN_DAY = 1439; // 23:59 in minutes
        public const float INITIAL_TIME_OF_DAY = 0.5f; // Midday

        [SerializeField] private float fullDayCycleInMinutes = 120;
        [SerializeField] private float transitionSpeed = 1f;
        [SerializeField] private float[] refreshIntervalByQuality;
        [field: SerializeField] public float RefreshInterval { get; set; } = 5f;

        private float timeOfDayNormalized;
        private bool isDayCycleEnabled;

        public event Action<float>? TimeOfDayChanged;
        public event Action<bool>? DayCycleChanged;

        public SkyboxRenderControllerRef SkyboxRenderControllerPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        public float FullCycleSpeed => 1f / (fullDayCycleInMinutes * 60f);
        public bool IsUIControlled { get; set; }
        public float UIOverrideTimeOfDayNormalized { get; set; }
        public Vector2Int? CurrentSDKControlledScene { get; set; }
        public bool IsDayCycleEnabled
        {
            get => isDayCycleEnabled;

            set
            {
                if (isDayCycleEnabled == value) return;
                isDayCycleEnabled = value;
                DayCycleChanged?.Invoke(value);
            }

        }
        public TransitionMode TransitionMode { get; set; }

        public float TransitionSpeed => transitionSpeed;

        public float TimeOfDayNormalized
        {
            get => timeOfDayNormalized;

            set
            {
                if (Mathf.Approximately(timeOfDayNormalized, value)) return;
                timeOfDayNormalized = value;
                TimeOfDayChanged?.Invoke(timeOfDayNormalized);
            }
        }

        public float TargetTimeOfDayNormalized { get; set; }

        public void Reset()
        {
            timeOfDayNormalized = INITIAL_TIME_OF_DAY;
            TargetTimeOfDayNormalized = INITIAL_TIME_OF_DAY;
            IsDayCycleEnabled = true;
            TransitionMode = TransitionMode.FORWARD;
            IsUIControlled = false;
            CurrentSDKControlledScene = null;
        }

        // Mapping: 0 - Low, 1 - Medium, 2 - High
        public void SetRefreshInterval(int qualityPresetId)
        {
            RefreshInterval = refreshIntervalByQuality[qualityPresetId];
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
