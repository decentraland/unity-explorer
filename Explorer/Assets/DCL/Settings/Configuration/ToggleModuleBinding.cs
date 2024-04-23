using DCL.Landscape.Settings;
using DCL.Quality;
using DCL.Settings.ModuleControllers;
using DCL.Settings.ModuleViews;
using ECS.Prioritization;
using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DCL.Settings.Configuration
{
    [Serializable]
    public class ToggleModuleBinding : SettingsModuleBinding<SettingsToggleModuleView, SettingsToggleModuleView.Config, ToggleModuleBinding.ToggleFeatures>
    {
        public enum ToggleFeatures
        {
            CHAT_SOUNDS_FEATURE,
            // add other features...
        }

        public override SettingsFeatureController CreateModule(
            Transform parent,
            RealmPartitionSettingsAsset realmPartitionSettingsAsset,
            LandscapeData landscapeData,
            AudioMixer generalAudioMixer,
            QualitySettingsAsset qualitySettingsAsset)
        {
            var viewInstance = UnityEngine.Object.Instantiate(View, parent);
            viewInstance.Configure(Config);

            switch (Feature)
            {
                case ToggleFeatures.CHAT_SOUNDS_FEATURE:
                    return new ChatSoundsSettingsController(viewInstance, generalAudioMixer);
                // add other cases...
            }

            throw new ArgumentOutOfRangeException(nameof(viewInstance));
        }
    }
}
