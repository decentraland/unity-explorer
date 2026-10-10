using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace DCL.VoiceChat.Permissions
{
    public class VoiceChatPermissions
    {
#if UNITY_STANDALONE_OSX
        public enum MicPermission
        {
            NOT_REQUESTED_YET = 0,
            AUTHORIZED = 1,
            DENIED = 2,
            RESTRICTED = 3,
            UNKNOWN = 4,
        }

        [DllImport("MicrophonePermissions")]
        private static extern void RequestMicrophonePermission();

        [DllImport("MicrophonePermissions")]
        private static extern int CurrentMicrophonePermission();

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/MicrophonePermissions/PrintStatus")]
        private static void PrintPermissionStatus(UnityEditor.MenuCommand command)
        {
            ReportHub.LogProductionInfo($"Permission status: {CurrentState()}");
        }
#endif

        private static void Request()
        {
            RequestMicrophonePermission();
        }

        public static MicPermission CurrentState()
        {
            return (MicPermission)CurrentMicrophonePermission();
        }

        public static async UniTask<bool> GuardAsync(CancellationToken token)
        {
            MicPermission state = CurrentState();

            if (state is MicPermission.DENIED)

                // cannot give permissions if already rejected
                return false;

            Request();

            await UniTask.WaitWhile(
                static () => CurrentState() is MicPermission.NOT_REQUESTED_YET,
                cancellationToken: token
            );

            return CurrentState() is MicPermission.AUTHORIZED;
        }
#endif
    }
}
