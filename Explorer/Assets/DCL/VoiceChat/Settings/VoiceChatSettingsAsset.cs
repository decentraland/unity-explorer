using LiveKit.Runtime.Scripts.Audio;

namespace DCL.Settings.Settings
{
    /// <summary>
    /// Exists as a single selection per time
    /// </summary>
    public static class VoiceChatSettings
    {
        public delegate void MicrophoneChangedDelegate(MicrophoneSelection newMicrophoneSelection);

        public static event MicrophoneChangedDelegate? MicrophoneChanged;

        public static MicrophoneSelection? SelectedMicrophone { get; private set; }

        public static void OnMicrophoneChanged(MicrophoneSelection microphoneSelection)
        {
            SelectedMicrophone = microphoneSelection;
            MicrophoneChanged?.Invoke(microphoneSelection);
        }
    }
}
