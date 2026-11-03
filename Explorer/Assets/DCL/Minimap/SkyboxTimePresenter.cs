using DCL.Prefs;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap
{
    /// <summary>
    ///     Binds the minimap skybox section (day-cycle toggle, time slider, time label) to <see cref="SkyboxSettingsAsset" />.
    /// </summary>
    public class SkyboxTimePresenter : IDisposable
    {
        private const int MINUTES_PER_HOUR = 60;

        private readonly Toggle timeProgressionToggle;
        private readonly Slider timeSlider;
        private readonly TMP_Text timeText;
        private readonly CanvasGroup sectionCanvasGroup;
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        private bool isRestrictedByScene;
        private int lastTotalMinutes = -1;

        public SkyboxTimePresenter(
            Toggle timeProgressionToggle,
            Slider timeSlider,
            TMP_Text timeText,
            CanvasGroup sectionCanvasGroup,
            SkyboxSettingsAsset skyboxSettings,
            ISceneRestrictionBusController sceneRestrictionBusController)
        {
            this.timeProgressionToggle = timeProgressionToggle;
            this.timeSlider = timeSlider;
            this.timeText = timeText;
            this.sectionCanvasGroup = sectionCanvasGroup;
            this.skyboxSettings = skyboxSettings;
            this.sceneRestrictionBusController = sceneRestrictionBusController;

            skyboxSettings.DayCycleChanged += OnDayCycleChanged;
            skyboxSettings.TimeOfDayChanged += OnTimeOfDayChanged;
            sceneRestrictionBusController.SubscribeToSceneRestriction(OnSceneRestrictionChanged);

            OnTimeOfDayChanged(skyboxSettings.TimeOfDayNormalized);
            timeProgressionToggle.SetIsOnWithoutNotify(skyboxSettings.IsDayCycleEnabled);
            timeSlider.interactable = !isRestrictedByScene && !skyboxSettings.IsDayCycleEnabled;

            timeProgressionToggle.onValueChanged.AddListener(OnTimeProgressionToggleChanged);
            timeSlider.onValueChanged.AddListener(OnTimeSliderValueChanged);
        }

        public void Dispose()
        {
            skyboxSettings.DayCycleChanged -= OnDayCycleChanged;
            skyboxSettings.TimeOfDayChanged -= OnTimeOfDayChanged;
            sceneRestrictionBusController.UnsubscribeToSceneRestriction(OnSceneRestrictionChanged);

            timeProgressionToggle.onValueChanged.RemoveListener(OnTimeProgressionToggleChanged);
            timeSlider.onValueChanged.RemoveListener(OnTimeSliderValueChanged);
        }

        private void OnDayCycleChanged(bool isEnabled) =>
            timeProgressionToggle.isOn = isEnabled;

        private void OnTimeOfDayChanged(float time)
        {
            timeSlider.SetValueWithoutNotify(time);

            var totalMinutes = (int)Mathf.Round(time * SkyboxSettingsAsset.TOTAL_MINUTES_IN_DAY);

            if (totalMinutes == lastTotalMinutes) return;

            lastTotalMinutes = totalMinutes;
            // ReSharper disable once PossibleLossOfFraction
            timeText.SetText("{0:00}:{1:00}", totalMinutes / MINUTES_PER_HOUR, totalMinutes % MINUTES_PER_HOUR);
        }

        private void OnTimeSliderValueChanged(float sliderValue)
        {
            skyboxSettings.TimeOfDayNormalized = sliderValue;
            skyboxSettings.TargetTimeOfDayNormalized = sliderValue;
            skyboxSettings.UIOverrideTimeOfDayNormalized = sliderValue;

            DCLPlayerPrefs.SetFloat(DCLPrefKeys.SKYBOX_FIXED_TIME, sliderValue);
        }

        /// <summary>
        ///     Automatic progression and the manual slider are mutually exclusive: turning the cycle on hands
        ///     control back to the simulation, turning it off lets the slider pin a time of day.
        /// </summary>
        private void OnTimeProgressionToggleChanged(bool isOn)
        {
            if (isRestrictedByScene) return;

            timeSlider.interactable = !isOn;
            skyboxSettings.IsUIControlled = !isOn;

            if (skyboxSettings.IsUIControlled)
            {
                float time = timeSlider.normalizedValue;
                skyboxSettings.UIOverrideTimeOfDayNormalized = time;

                DCLPlayerPrefs.SetFloat(DCLPrefKeys.SKYBOX_FIXED_TIME, time);
            }
            else
                DCLPlayerPrefs.DeleteKey(DCLPrefKeys.SKYBOX_FIXED_TIME);
        }

        private void OnSceneRestrictionChanged(SceneRestriction restriction)
        {
            if (restriction.Type != SceneRestrictions.SkyboxTimeUiBlocked) return;

            isRestrictedByScene = restriction.Action == SceneRestrictionsAction.Applied;

            timeProgressionToggle.interactable = !isRestrictedByScene;
            timeSlider.interactable = !isRestrictedByScene && !timeProgressionToggle.isOn;
            sectionCanvasGroup.interactable = !isRestrictedByScene;
        }
    }
}
