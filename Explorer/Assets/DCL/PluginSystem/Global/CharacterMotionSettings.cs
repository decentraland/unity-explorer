using DCL.AssetsProvision;
using DCL.CharacterMotion.Settings;
using ECS.Unity.GliderProp;
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
            public GliderPropPrefabReference PropPrefab { get; private set; }

            [field:SerializeField]
            public bool EnablePropPooling { get; private set; }

            [field: SerializeField]
            public float TrailVelocityThreshold { get; private set; } = 1;

            [Serializable]
            public class GliderPropPrefabReference : ComponentReference<GliderPropView>
            {
                public GliderPropPrefabReference(string guid) : base(guid)
                {
                }
            }
        }
    }
}
