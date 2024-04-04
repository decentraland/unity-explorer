using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
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
        public abstract SettingsFeatureController CreateModule(Transform parent);
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

    [Serializable]
    public class ToggleModuleBinding : SettingsModuleBinding<SettingsToggleModuleView, SettingsToggleModuleView.Config, ToggleModuleBinding.ToggleFeatures>
    {
        public enum ToggleFeatures
        {
            ToggleModuleFeature1,
            // add other features...
        }

        public override SettingsFeatureController CreateModule(Transform parent)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case ToggleFeatures.ToggleModuleFeature1:
                    return new Example1Controller(View);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(View));
        }
    }

    [Serializable]
    public class SliderModuleBinding : SettingsModuleBinding<SettingsSliderModuleView, SettingsSliderModuleView.Config, SliderModuleBinding.SliderFeatures>
    {
        public enum SliderFeatures
        {
            SliderModuleFeature1,
            // add other features...
        }

        public override SettingsFeatureController CreateModule(Transform parent)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case SliderFeatures.SliderModuleFeature1:
                    return new Example2Controller(View);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(View));
        }
    }

    [Serializable]
    public class DropdownModuleBinding : SettingsModuleBinding<SettingsDropdownModuleView, SettingsDropdownModuleView.Config, DropdownModuleBinding.DropdownFeatures>
    {
        public enum DropdownFeatures
        {
            DropdownModuleFeature1,
            // add other features...
        }

        public override SettingsFeatureController CreateModule(Transform parent)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case DropdownFeatures.DropdownModuleFeature1:
                    return new Example3Controller(View);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(View));
        }
    }
}
