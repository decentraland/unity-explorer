using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.IO;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.RuntimeDeepLink
{
    public static class DeepLinkSentinel
    {
        private static readonly TimeSpan CHECK_IN_PERIOD = TimeSpan.FromMilliseconds(200);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        // path for: C:\Users\<YourUsername>\AppData\Local\DecentralandLauncherLight\
        private static readonly string DEEP_LINK_BRIDGE_PATH =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DecentralandLauncherLight", "deeplink-bridge.json"
            );
#else

        // path for: ~/Library/Application Support/DecentralandLauncherLight/
        private static readonly string DEEP_LINK_BRIDGE_PATH =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library", "Application Support", "DecentralandLauncherLight", "deeplink-bridge.json"
            );
#endif

        [Serializable]
        private struct DeepLinkDTO
        {
            public string? deeplink;
        }

        /// <summary>
        /// Runs for the lifetime of the app.
        /// </summary>
        public static async UniTaskVoid StartListenForDeepLinksAsync(this DeepLinkController controller, CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                bool delayResult = await UniTask.Delay(CHECK_IN_PERIOD, cancellationToken: token).SuppressCancellationThrow();

                // Delay was cancelled
                if (delayResult == false) continue;

                // File.Exists method is lightweight and can be used in this loop
                if (!File.Exists(DEEP_LINK_BRIDGE_PATH)) continue;

                Result<string> contentResult = await File.ReadAllTextAsync(DEEP_LINK_BRIDGE_PATH, token)!.SuppressToResultAsync<string>(ReportCategory.RUNTIME_DEEPLINKS);
                if (contentResult.Success == false) continue;

                // Notify emitter that file has been consumed
                File.Delete(DEEP_LINK_BRIDGE_PATH);

                DeepLinkDTO dto = JsonUtility.FromJson<DeepLinkDTO>(contentResult.Value);
                string? raw = dto.deeplink;

                Result deepLinkHandleResult = controller.HandleDeepLink(raw);

                if (deepLinkHandleResult.Success)
                    ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, $"{controller.GetType().Name} successfully handled deeplink");
                else
                    ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, deepLinkHandleResult.ErrorMessage!);
            }
        }
    }
}
