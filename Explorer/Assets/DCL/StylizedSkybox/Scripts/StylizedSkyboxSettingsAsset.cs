using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.StylizedSkybox.Scripts
{
    [CreateAssetMenu(menuName = "DCL/SO/Stylized Skybox Settings", fileName = "StylizedSkyboxSettings")]
    public class StylizedSkyboxSettingsAsset : ScriptableObject
    {
        public int SECONDS_IN_DAY = 86400;
        
        public StylizedSkyboxControllerRef StylizedSkyboxPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;
        public event Action<float> NormalizedTimeChanged;
        public event Action<bool> UseDynamicTimeChanged;
        public float FixedTime { get; private set; }
        public bool IsFixedTime { get; private set; }
        
        private float normalizedTime;

        public float NormalizedTime
        {
            get => normalizedTime;

            set
            {
                if (normalizedTime == value) return;

                normalizedTime = value;
                NormalizedTimeChanged?.Invoke(value);
            }
        }

        private bool useDynamicTime = true;
        public bool UseDynamicTime
        {
            get => useDynamicTime;

            set
            {
                if (useDynamicTime == value) return;

                useDynamicTime = value;
                UseDynamicTimeChanged?.Invoke(value);
            }
        }
        
        /// <summary>
        /// Configures a fixed skybox time.
        /// </summary>
        public void ApplyFixedTime(float secondsOfDay)
        {
            FixedTime = secondsOfDay;
            IsFixedTime = true;
            UseDynamicTime = false;
        }

        /// <summary>
        /// Switches back to dynamic (real-time) skybox cycling.
        /// </summary>
        public void ApplyDynamicTime()
        {
            IsFixedTime = false;
            UseDynamicTime = true;
        }

        [Serializable]
        public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
        {
            public StylizedSkyboxControllerRef(string guid) : base(guid) { }
        }
    }
}
