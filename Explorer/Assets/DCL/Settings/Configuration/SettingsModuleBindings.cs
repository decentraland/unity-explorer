using DCL.Landscape.Settings;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using ECS.Prioritization;
using System;
using UnityEngine;

namespace DCL.Settings.Configuration
{
    /// <summary>
    /// We need this class to serialize by ref
    /// </summary>
    [Serializable]
    public abstract class SettingsModuleBindingBase
    {
        public abstract SettingsFeatureController CreateModule(
            Transform parent,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            LandscapeData landscapeData);
    }

    [Serializable]
    public abstract class SettingsModuleBinding<TView, TConfig, TControllerType> : SettingsModuleBindingBase
        where TView : SettingsModuleView<TConfig>
        where TConfig : SettingsModuleViewConfiguration
        where TControllerType : Enum
    {
        [field: SerializeField]
        public TView View { get; private set; }

        [field: SerializeField]
        public TConfig Config { get; private set; }

        [field: SerializeField]
        public TControllerType Feature { get; private set; }
    }
}
