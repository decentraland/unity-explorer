using DCL.AssetsProvision;
using DCL.SceneLoadingScreens;
using System;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public partial class LoadingScreenPlugin
    {
        public struct LoadingScreenPluginSettings : IDCLPluginSettings
        {
            [field: Header(nameof(LoadingScreenPlugin) + "." + nameof(Web3AuthPluginSettings))]
            [field: Space]
            [field: SerializeField]
            public LoadingScreenObjectRef LoadingScreenPrefab { get; private set; }

            [field: SerializeField]
            public string FallbackTipsTable { get; private set; }

            [field: SerializeField]
            public string FallbackImagesTable { get; private set; }

            [field: SerializeField]
            public float TipDisplayDuration { get; private set; }

            [field: SerializeField]
            public float MinimumScreenDisplayDuration { get; private set; }

            [Serializable]
            public class LoadingScreenObjectRef : ComponentReference<SceneLoadingScreenView>
            {
                public LoadingScreenObjectRef(string guid) : base(guid) { }
            }
        }
    }
}
