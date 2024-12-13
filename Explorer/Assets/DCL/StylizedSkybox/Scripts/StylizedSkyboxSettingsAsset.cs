using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.StylizedSkybox.Scripts.Plugin
{
    [CreateAssetMenu(menuName = "Create Stylized Skybox Settings", fileName = "StylizedSkyboxSettings", order = 0)]
    public class StylizedSkyboxSettingsAsset: ScriptableObject
    {
        public StylizedSkyboxControllerRef StylizedSkyboxPrefab = null!;
        public Material SkyboxMaterial = null!;
        public AssetReferenceT<AnimationClip> SkyboxAnimationCycle = null!;

        public event Action<int> TimeOfDayChanged;

        private int timeOfDay;
        public int TimeOfDay
        {
            get => timeOfDay;

            set
            {
                timeOfDay = value;
                TimeOfDayChanged?.Invoke(value);
            }
        }

        [Serializable]
        public class StylizedSkyboxControllerRef : ComponentReference<SkyboxController>
        {
            public StylizedSkyboxControllerRef(string guid) : base(guid) { }
        }
    }
}
