using DCL.AssetsProvision;
using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static DCL.Ipfs.SceneMetadata;

namespace DCL.SkyBox
{
    [CreateAssetMenu(menuName = "DCL/SO/Skybox Settings", fileName = "SkyboxSettings")]
    public class SkyboxSettingsAsset : ScriptableObject
    {
        private const int SECONDS_IN_DAY = 86400;
        // We need to subtract 1 minute to make the slider range is between 00:00 and 23:59
        public const int TOTAL_MINUTES_IN_DAY = 1439; // 23:59 in minutes

        [SerializeField] private float fullDayCycleInSeconds = 120 * 60;
        [SerializeField] private float transitionSpeed = 1f;
        [SerializeField] private float[] refreshIntervalByQuality = null!;

        public float RefreshInterval => refreshIntervalByQuality[refreshIntervalId];

        private uint refreshIntervalId;
        private float timeOfDayNormalized;
        private bool isDayCycleEnabled;

        public event Action<float>? TimeOfDayChanged;
        public event Action<bool>? DayCycleChanged;

        public SkyboxRenderControllerRef SkyboxRenderControllerPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        [Tooltip("Alternative looks, loaded only when needed: picked by name through the skybox look preset feature flag, or from the debug panel dropdown. The default look is the prefab's own preset and is not listed here.")]
        public LookPresetEntry[] LookPresets = Array.Empty<LookPresetEntry>();

        public float FullDayCycleInSeconds
        {
            get => fullDayCycleInSeconds;
            set => fullDayCycleInSeconds = value;
        }

        public bool IsUIControlled { get; set; }
        public float UIOverrideTimeOfDayNormalized { get; set; }
        public SceneShortInfo? CurrentSDKControlledScene { get; set; }
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

        /// <summary>
        /// Normalized time of day (0-1).
        /// Setting this property also updates TimeOfDayInSeconds.
        /// </summary>
        public float TimeOfDayNormalized
        {
            get => timeOfDayNormalized;
            set
            {
                if (Mathf.Approximately(timeOfDayNormalized, value)) return;
                timeOfDayNormalized = value;
                TimeOfDayInSeconds = (uint)(value * SECONDS_IN_DAY);
                TimeOfDayChanged?.Invoke(timeOfDayNormalized);
            }
        }

        public uint TimeOfDayInSeconds { get; private set; }

        public float TargetTimeOfDayNormalized { get; set; }

        public float GlobalTimeOfDayNormalized
        {
            get
            {
                var span = new TimeSpan(DateTime.UtcNow.Ticks);
                double norm = span.TotalSeconds % FullDayCycleInSeconds;
                return (float) (norm / FullDayCycleInSeconds);
            }
        }

        public static float NormalizeTime(float time)
        {
            if (time < 0)
                return 0;

            time %= SECONDS_IN_DAY;
            return time / SECONDS_IN_DAY;
        }

        // Mapping: 0 - Low, 1 - Medium, 2 - High, 3 - Custom
        public void SetRefreshInterval(int qualityPresetId)
        {
            refreshIntervalId = (uint)Math.Min(qualityPresetId, refreshIntervalByQuality.Length - 1);
        }

        // Compares the full scene identity: base parcels are not unique, portable experiences usually share (0,0) with world scenes
        public bool IsSDKControlledBy(SceneShortInfo scene) =>
            CurrentSDKControlledScene is { } owner && owner.Equals(scene);

        /// <summary>
        ///     Index of the <see cref="LookPresets" /> entry with this name, ignoring case and surrounding whitespace; -1 when none matches.
        /// </summary>
        public int IndexOfLookPreset(string presetName)
        {
            string trimmed = presetName.Trim();

            for (var i = 0; i < LookPresets.Length; i++)
                if (string.Equals(LookPresets[i].Name, trimmed, StringComparison.OrdinalIgnoreCase))
                    return i;

            return -1;
        }

        public void Reset()
        {
            TimeOfDayNormalized = GlobalTimeOfDayNormalized;
            TargetTimeOfDayNormalized = GlobalTimeOfDayNormalized;
            IsDayCycleEnabled = true;
            TransitionMode = TransitionMode.FORWARD;
            IsUIControlled = false;
            CurrentSDKControlledScene = null;
        }

        [Serializable]
        public class SkyboxRenderControllerRef : ComponentReference<SkyboxRenderController>
        {
            public SkyboxRenderControllerRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class LookPresetEntry
        {
            public string Name = string.Empty;
            public AssetReferenceT<SkyboxLookPreset> Preset = null!;
        }
    }
}
