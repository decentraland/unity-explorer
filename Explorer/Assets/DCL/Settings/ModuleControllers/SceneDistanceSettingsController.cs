using DCL.Prefs;
using DCL.Settings.ModuleViews;
using ECS.Prioritization;

namespace DCL.Settings.ModuleControllers
{
    public class SceneDistanceSettingsController : SettingsFeatureController
    {
        private readonly SettingsSliderModuleView view;
        private readonly RealmPartitionSettingsAsset realmPartitionSettingsAsset;

        public SceneDistanceSettingsController(SettingsSliderModuleView view, RealmPartitionSettingsAsset realmPartitionSettingsAsset)
        {
            this.view = view;
            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;

            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_SCENE_DISTANCE))
                view.SliderView.Slider.value = DCLPlayerPrefs.GetSliderValue(DCLPrefKeys.SETTINGS_SCENE_DISTANCE);

            view.SliderView.Slider.onValueChanged.AddListener(SetSceneDistanceSettings);
            SetSceneDistanceSettings(view.SliderView.Slider.value);

            realmPartitionSettingsAsset.OnMaxLoadingDistanceInParcelsChanged += OnMaxLoadingDistanceInParcelsChangedChanged;
        }

        private void SetSceneDistanceSettings(float distance) =>
            realmPartitionSettingsAsset.MaxLoadingDistanceInParcels = (int)distance;

        private void OnMaxLoadingDistanceInParcelsChangedChanged(int newDistance)
        {
            view.SliderView.Slider.value = newDistance;
            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_SCENE_DISTANCE, newDistance, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetSceneDistanceSettings);
            realmPartitionSettingsAsset.OnMaxLoadingDistanceInParcelsChanged -= OnMaxLoadingDistanceInParcelsChangedChanged;
        }
    }
}
