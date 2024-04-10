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
            CHAT_SOUNDS_FEATURE,
            // add other features...
        }

        public override SettingsFeatureController CreateModule(Transform parent)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case ToggleFeatures.CHAT_SOUNDS_FEATURE:
                    return new ChatSoundsSettingsController(viewInstance);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(viewInstance));
        }
    }

    [Serializable]
    public class SliderModuleBinding : SettingsModuleBinding<SettingsSliderModuleView, SettingsSliderModuleView.Config, SliderModuleBinding.SliderFeatures>
    {
        public enum SliderFeatures
        {
            SCENE_DISTANCE_FEATURE,
            ENVIRONMENT_DISTANCE_FEATURE,
            MOUSE_SENSITIVITY_FEATURE,
            MASTER_VOLUME_FEATURE,
            WORLD_SOUNDS_VOLUME_FEATURE,
            AVATAR_SOUNDS_VOLUME_FEATURE,
            UI_SOUNDS_VOLUME_FEATURE,
            // add other features...
        }

        public override SettingsFeatureController CreateModule(Transform parent)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case SliderFeatures.SCENE_DISTANCE_FEATURE:
                    return new SceneDistanceSettingsController(viewInstance);
                case SliderFeatures.ENVIRONMENT_DISTANCE_FEATURE:
                    return new EnvironmentDistanceSettingsController(viewInstance);
                case SliderFeatures.MOUSE_SENSITIVITY_FEATURE:
                    return new MouseSensitivitySettingsController(viewInstance);
                case SliderFeatures.MASTER_VOLUME_FEATURE:
                    return new MasterVolumeSettingsController(viewInstance);
                case SliderFeatures.WORLD_SOUNDS_VOLUME_FEATURE:
                    return new WorldSoundsVolumeSettingsController(viewInstance);
                case SliderFeatures.AVATAR_SOUNDS_VOLUME_FEATURE:
                    return new AvatarSoundsVolumeSettingsController(viewInstance);
                case SliderFeatures.UI_SOUNDS_VOLUME_FEATURE:
                    return new UISoundsVolumeSettingsController(viewInstance);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(viewInstance));
        }
    }

    [Serializable]
    public class DropdownModuleBinding : SettingsModuleBinding<SettingsDropdownModuleView, SettingsDropdownModuleView.Config, DropdownModuleBinding.DropdownFeatures>
    {
        public enum DropdownFeatures
        {
            GRAPHICS_QUALITY_FEATURE,
            CAMERA_LOCK_FEATURE,
            CAMERA_SHOULDER_FEATURE
            // add other features...
        }

        public override SettingsFeatureController CreateModule(Transform parent)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case DropdownFeatures.GRAPHICS_QUALITY_FEATURE:
                    return new GraphicsQualitySettingsController(viewInstance, Config.defaultOptionIndex);
                case DropdownFeatures.CAMERA_LOCK_FEATURE:
                    return new CameraLockSettingsController(viewInstance);
                case DropdownFeatures.CAMERA_SHOULDER_FEATURE:
                    return new CameraShoulderSettingsController(viewInstance);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(viewInstance));
        }
    }
}
