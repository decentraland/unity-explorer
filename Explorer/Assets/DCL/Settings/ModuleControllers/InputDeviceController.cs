using DCL.Diagnostics;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class InputDeviceController : SettingsFeatureController
    {
        private readonly SettingsDropdownModuleView view;

        public InputDeviceController(SettingsDropdownModuleView view)
        {
            this.view = view;

            LoadInputDeviceOptions();
            SetSelection();

            AudioSettings.OnAudioConfigurationChanged += AudioConfigChanged;
            view.DropdownView.Dropdown.onValueChanged.AddListener(ApplySettings);
        }

        private void ApplySettings(int pickedMicrophoneIndex)
        {
            Result<MicrophoneSelection> result = MicrophoneSelection.FromIndex(pickedMicrophoneIndex);

            if (result.Success == false)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Picked invalid selection from ui: {result.ErrorMessage}");
                return;
            }

            MicrophoneSelection microphoneSelection = result.Value;

            DCLPlayerPrefs.SetString(DCLPrefKeys.SETTINGS_MICROPHONE_DEVICE_NAME, microphoneSelection.name);
            VoiceChatSettings.OnMicrophoneChanged(microphoneSelection);
        }

        private void AudioConfigChanged(bool deviceWasChanged)
        {
            if (!deviceWasChanged) return;

            LoadInputDeviceOptions();
            SetSelection();
        }

        private void SetSelection()
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MICROPHONE_DEVICE_NAME))
            {
                string microphoneName = DCLPlayerPrefs.GetString(DCLPrefKeys.SETTINGS_MICROPHONE_DEVICE_NAME);
                string[] devices = MicrophoneSelection.Devices();

                for (var i = 0; i < devices.Length; i++)
                {
                    if (!devices[i].Equals(microphoneName)) continue;

                    UpdateDropdownSelection(i);
                    return;
                }
            }

            UpdateDropdownSelection(0);
        }

        private void UpdateDropdownSelection(int index)
        {
            Result<MicrophoneSelection> result = MicrophoneSelection.FromIndex(index);

            if (result.Success == false)
            {
                ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Picked invalid selection from ui: {result.ErrorMessage}");
                return;
            }

            MicrophoneSelection microphoneSelection = result.Value;

            view.DropdownView.Dropdown.value = index;
            VoiceChatSettings.OnMicrophoneChanged(microphoneSelection);
            view.DropdownView.Dropdown.RefreshShownValue();
        }

        private void LoadInputDeviceOptions()
        {
            view.DropdownView.Dropdown.options.Clear();

            foreach (string option in MicrophoneSelection.Devices())
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });
        }

        public override void Dispose()
        {
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigChanged;
            view.DropdownView.Dropdown.onValueChanged.RemoveAllListeners();
        }
    }
}
