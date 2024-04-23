using DCL.Settings.ModuleViews;
using ECS.Prioritization;

namespace DCL.Settings.ModuleControllers
{
    public class SceneDistanceSettingsController : SettingsFeatureController
    {
        private const string SCENE_DISTANCE_DATA_STORE_KEY = "Settings_SceneDistance";

        private readonly SettingsSliderModuleView view;
        private readonly RealmPartitionSettingsAsset realmPartitionSettingsAsset;

        public SceneDistanceSettingsController(SettingsSliderModuleView view, RealmPartitionSettingsAsset realmPartitionSettingsAsset)
        {
            this.view = view;
            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;

            if (settingsDataStore.HasKey(SCENE_DISTANCE_DATA_STORE_KEY))
                view.SliderView.Slider.value = settingsDataStore.GetSliderValue(SCENE_DISTANCE_DATA_STORE_KEY);

            view.SliderView.Slider.onValueChanged.AddListener(SetSceneDistanceSettings);
            SetSceneDistanceSettings(view.SliderView.Slider.value);

            realmPartitionSettingsAsset.OnMaxLoadingDistanceInParcelsChanged += OnMaxLoadingDistanceInParcelsChangedChanged;
        }

        private void SetSceneDistanceSettings(float distance) =>
            realmPartitionSettingsAsset.MaxLoadingDistanceInParcels = (int)distance;

        private void OnMaxLoadingDistanceInParcelsChangedChanged(int newDistance)
        {
            view.SliderView.Slider.value = newDistance;
            settingsDataStore.SetSliderValue(SCENE_DISTANCE_DATA_STORE_KEY, newDistance, save: true);
        }

        public override void Dispose()
        {
            view.SliderView.Slider.onValueChanged.RemoveListener(SetSceneDistanceSettings);
            realmPartitionSettingsAsset.OnMaxLoadingDistanceInParcelsChanged -= OnMaxLoadingDistanceInParcelsChangedChanged;
        }
    }
}
