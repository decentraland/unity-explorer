using Cysharp.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;

namespace DCL.VoiceChat.Permissions
{
    public class VoiceChatPermissions
    {
#if UNITY_STANDALONE_OSX
        private enum MicPermission
        {
            NOT_REQUESTED_YET = 0,
            GRANTED = 1,
            REJECTED = 2,
        }

        [DllImport("__Internal")]
        private static extern void RequestMicrophonePermission();

        [DllImport("__Internal")]
        private static extern int CurrentMicrophonePermission();

        private static void Request()
        {
            RequestMicrophonePermission();
        }

        private static MicPermission CurrentState()
        {
            return (MicPermission)CurrentMicrophonePermission();
        }

        public static async UniTask<bool> GuardAsync(CancellationToken token)
        {
            MicPermission state = CurrentState();

            if (state is MicPermission.REJECTED)

                // cannot give permissions if already rejected
                return false;

            Request();

            await UniTask.WaitWhile(
                static () => CurrentState() is MicPermission.NOT_REQUESTED_YET,
                cancellationToken: token
            );

            return CurrentState() is MicPermission.GRANTED;
        }
#endif
    }
}
