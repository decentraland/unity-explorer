using DCL.CharacterMotion.Settings;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class CharacterMotionSettings : IDCLPluginSettings
    {
        [field: Header(nameof(CharacterMotionSettings))]
        [field: Space]
        [field: SerializeField]
        public CharacterControllerSettings ControllerSettings { get; private set; }

        [field: SerializeField]
        public GlidingSettings Gliding { get; private set; }

        [Serializable]
        public class GlidingSettings
        {
            [field: SerializeField]
            public AssetReferenceT<GameObject> PropPrefab { get; private set; }

            [field: SerializeField]
            public float AnimationSpeed { get; private set; }= 2;

            [field: SerializeField]
            public float StartingRotation { get; private set; } = -60;

            [field: SerializeField]
            [field: Range(0, 1)]
            public float CompleteRotationT { get; private set; } = 0.5f;

            [field: SerializeField]
            public float TrailVelocityThreshold { get; private set; } = 1;
        }
    }
}
