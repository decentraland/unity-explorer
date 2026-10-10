using DCL.DebugUtilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Quality.Runtime
{
    public class QualityLevelController : IQualityLevelController
    {
        private readonly IReadOnlyList<IQualitySettingRuntime> runtimes;
        private readonly IReadOnlyList<QualitySettingsAsset.QualityCustomLevel> customLevels;

        public QualityLevelController(IReadOnlyList<IQualitySettingRuntime> runtimes, IReadOnlyList<QualitySettingsAsset.QualityCustomLevel> customLevels)
        {
            this.runtimes = runtimes;
            this.customLevels = customLevels;

            int currentQuality = QualitySettings.GetQualityLevel();

            if (customLevels.Count > currentQuality)
            {
                QualitySettingsAsset.QualityCustomLevel currentPreset = customLevels[currentQuality];

                foreach (IQualitySettingRuntime? runtime in runtimes)
                    runtime.RestoreState(currentPreset);
            }

            // Make a subscription so the quality level controller can react to changes done from Unity itself (any source)
            QualitySettings.activeQualityLevelChanged += OnQualityLevelChanged;
        }

        public void Dispose()
        {
            QualitySettings.activeQualityLevelChanged -= OnQualityLevelChanged;

            foreach (IQualitySettingRuntime runtime in runtimes)
                runtime.Dispose();
        }

        private void OnQualityLevelChanged(int from, int to)
        {
            if (customLevels.Count <= to)
                return;

            QualitySettingsAsset.QualityCustomLevel newPreset = customLevels[to];

            foreach (IQualitySettingRuntime? runtime in runtimes)
                runtime.ApplyPreset(newPreset);
        }

        public void SetLevel(int index)
        {
            QualitySettings.SetQualityLevel(index, true);
        }

        public void AddDebugViews(DebugWidgetBuilder debugWidgetBuilder, List<Action> onUpdate)
        {
            foreach (IQualitySettingRuntime? settingRuntime in runtimes)
                settingRuntime.AddDebugView(debugWidgetBuilder, onUpdate);

            debugWidgetBuilder.AddSingleButton("TOGGLE MEDIA PLAYERS", ToggleMediaPlayers);
        }

        private static void ToggleMediaPlayers()
        {
            // Debugging purposes only, it should be OK to hardcode the game object name
            const string containerName = "POOL_CONTAINER_MEDIA_PLAYER";

            var container = GameObject.Find(containerName);
            if (!container)
            {
                UnityEngine.Debug.LogError($"Game Object with name '{containerName}' not found");
                return;
            }

            // We do not toggle the container directly because GameObject.Find would fail the next time we invoke it
            foreach (Transform child in container.transform)
                child.gameObject.SetActive(!child.gameObject.activeSelf);
        }
    }
}
