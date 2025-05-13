using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using TMPro;
using UnityEngine;

namespace DCL.Settings.ModuleControllers
{
    public class InputDeviceController : SettingsFeatureController
    {
        private const string MICROPHONE_DEVICE_NAME = "Settings_MicrophoneName";
        private readonly SettingsDropdownModuleView view;
        private readonly VoiceChatSettingsAsset voiceChatSettings;

        public InputDeviceController(SettingsDropdownModuleView view, VoiceChatSettingsAsset voiceChatSettings)
        {
            this.view = view;
            this.voiceChatSettings = voiceChatSettings;

            LoadInputDeviceOptions();
            SetSelection();

            AudioSettings.OnAudioConfigurationChanged += AudioConfigChanged;
            view.DropdownView.Dropdown.onValueChanged.AddListener(ApplySettings);
        }

        private void ApplySettings(int pickedMicrophoneIndex)
        {
            settingsDataStore.SetStringValue(MICROPHONE_DEVICE_NAME, Microphone.devices[pickedMicrophoneIndex], true);
            voiceChatSettings.SelectedMicrophoneIndex = pickedMicrophoneIndex;
        }

        private void AudioConfigChanged(bool deviceWasChanged)
        {
            if (!deviceWasChanged) return;

            LoadInputDeviceOptions();
            SetSelection();
        }

        private void SetSelection()
        {
            if (settingsDataStore.HasKey(MICROPHONE_DEVICE_NAME))
            {
                string microphoneName = settingsDataStore.GetStringValue(MICROPHONE_DEVICE_NAME);

                for (var i = 0; i < Microphone.devices.Length; i++)
                {
                    if (!Microphone.devices[i].Equals(microphoneName)) continue;

                    UpdateDropdownSelection(i);
                    return;
                }
            }

            UpdateDropdownSelection(0);
        }

        private void UpdateDropdownSelection(int index)
        {
            view.DropdownView.Dropdown.value = index;
            voiceChatSettings.SelectedMicrophoneIndex = index;
            view.DropdownView.Dropdown.RefreshShownValue();
        }

        private void LoadInputDeviceOptions()
        {
            view.DropdownView.Dropdown.options.Clear();
            foreach (string option in Microphone.devices)
                view.DropdownView.Dropdown.options.Add(new TMP_Dropdown.OptionData { text = option });
        }

        public override void Dispose()
        {
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigChanged;
            view.DropdownView.Dropdown.onValueChanged.RemoveAllListeners();
        }
    }
}
