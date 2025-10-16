using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Settings;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;
using System;
using System.Threading;
using TMPro;

namespace DCL.Settings.ModuleControllers
{
    public class InputDeviceController : SettingsFeatureController
    {
        private readonly ExtendedObjectPool<TMP_Dropdown.OptionData> optionsPool = new (static () => new TMP_Dropdown.OptionData());
        private readonly SettingsDropdownModuleView view;

        private CancellationTokenSource? requestDeviceStatusCancellationToken;

        public InputDeviceController(SettingsDropdownModuleView view)
        {
            this.view = view;

            string[] devices = MicrophoneSelection.Devices();
            LoadInputDeviceOptions(devices);
            SetSelection(devices);

            view.DropdownView.Dropdown.onValueChanged.AddListener(ApplySettings);
            view.showStatusUpdated.AddListener(OnShowStatusUpdated);
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

        private void OnShowStatusUpdated(bool isShown)
        {
            if (isShown == false)
            {
                requestDeviceStatusCancellationToken?.Cancel();
                requestDeviceStatusCancellationToken = null;
                return;
            }

            TimeSpan updatePoll = TimeSpan.FromMilliseconds(500);
            requestDeviceStatusCancellationToken = new CancellationTokenSource();
            BackgroundTaskAsync(requestDeviceStatusCancellationToken.Token).Forget();
            return;

            async UniTaskVoid BackgroundTaskAsync(CancellationToken token)
            {
                string[] lastDevices = Array.Empty<string>();

                while (token.IsCancellationRequested == false)
                {
                    await UniTask.Delay(updatePoll, cancellationToken: token).SuppressCancellationThrow();
                    string[] devices = MicrophoneSelection.Devices();

                    bool same = AreEquals(devices, lastDevices);
                    if (same) continue;

                    lastDevices = devices;

                    LoadInputDeviceOptions(devices);
                    SetSelection(devices);
                }

                static bool AreEquals(string[] a, string[] b)
                {
                    if (a.Length != b.Length)
                        return false;

                    for (int i = 0; i < a.Length; i++)
                        if (!string.Equals(a[i], b[i]))
                            return false;

                    return true;
                }
            }
        }

        private void SetSelection(string[] devices)
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MICROPHONE_DEVICE_NAME))
            {
                string microphoneName = DCLPlayerPrefs.GetString(DCLPrefKeys.SETTINGS_MICROPHONE_DEVICE_NAME);

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
            BackgroundTaskAsync().Forget();
            return;

            async UniTaskVoid BackgroundTaskAsync()
            {
                await UniTask.SwitchToThreadPool();

                Result<MicrophoneSelection> result = MicrophoneSelection.FromIndex(index);

                if (result.Success == false)
                {
                    ReportHub.LogError(ReportCategory.VOICE_CHAT, $"Picked invalid selection from ui: {result.ErrorMessage}");
                    return;
                }

                MicrophoneSelection microphoneSelection = result.Value;

                await UniTask.SwitchToMainThread();

                view.DropdownView.Dropdown.value = index;
                view.DropdownView.Dropdown.RefreshShownValue();
                VoiceChatSettings.OnMicrophoneChanged(microphoneSelection);
            }
        }

        private void LoadInputDeviceOptions(string[] devices)
        {
            foreach (TMP_Dropdown.OptionData dropdownOption in view.DropdownView.Dropdown.options) optionsPool.Release(dropdownOption);
            view.DropdownView.Dropdown.options.Clear();

            foreach (string option in devices)
            {
                var data = optionsPool.Get();
                data!.text = option;
                view.DropdownView.Dropdown.options.Add(data);
            }
        }

        public override void Dispose()
        {
            view.DropdownView.Dropdown.onValueChanged.RemoveAllListeners();
            view.showStatusUpdated.RemoveListener(OnShowStatusUpdated);
            optionsPool.Dispose();
        }
    }
}
