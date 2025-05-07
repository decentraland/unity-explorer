using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.StylizedSkybox.Scripts
{
    [CreateAssetMenu(menuName = "DCL/SO/Stylized Skybox Settings", fileName = "StylizedSkyboxSettings")]
    public class StylizedSkyboxSettingsAsset : ScriptableObject
    {
        public StylizedSkyboxControllerRef StylizedSkyboxPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        public event Action<float> NormalizedTimeChanged;
        public event Action<bool> UseDynamicTimeChanged;

        public float FixedTime { get; set; }
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

        [Serializable]
        public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
        {
            public StylizedSkyboxControllerRef(string guid) : base(guid) { }
        }
    }
}
