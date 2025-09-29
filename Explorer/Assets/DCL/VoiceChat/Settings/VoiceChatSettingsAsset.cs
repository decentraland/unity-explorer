using DCL.Diagnostics;
using LiveKit.Runtime.Scripts.Audio;
using RichTypes;

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

        public static Result<MicrophoneSelection> ReachableSelection()
        {
            if (SelectedMicrophone == null)
                return TrySelectDefault();

            string current = SelectedMicrophone.Value.name;
            Result<MicrophoneSelection> currentResult = MicrophoneSelection.FromName(current);

            if (currentResult.Success == false)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"Current microphone is unreachable, fallback to default: {current}");
                return TrySelectDefault();
            }

            SelectedMicrophone = currentResult.Value;
            return Result<MicrophoneSelection>.SuccessResult(SelectedMicrophone.Value);
        }

        private static Result<MicrophoneSelection> TrySelectDefault()
        {
            Result<MicrophoneSelection> result = MicrophoneSelection.Default();

            if (result.Success == false)
                return Result<MicrophoneSelection>.ErrorResult("No reachable microphone is available");

            SelectedMicrophone = result.Value;
            return Result<MicrophoneSelection>.SuccessResult(SelectedMicrophone.Value);
        }
    }
}
