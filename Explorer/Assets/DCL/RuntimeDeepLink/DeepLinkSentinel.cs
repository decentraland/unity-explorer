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


        /// <summary>
        /// Runs for the lifetime of the app.
        /// </summary>
        public static async UniTaskVoid StartListenForDeepLinksAsync(this IDeepLinkHandle handle, CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                bool cancelled = await UniTask.Delay(CHECK_IN_PERIOD, cancellationToken: token).SuppressCancellationThrow();
                if (cancelled) continue;

                // File.Exists method is lightweight and can be used in this loop
                if (!File.Exists(DEEP_LINK_BRIDGE_PATH)) continue;

                Result<string> contentResult = await File.ReadAllTextAsync(DEEP_LINK_BRIDGE_PATH, token)!.SuppressToResultAsync<string>(ReportCategory.RUNTIME_DEEPLINKS);
                if (contentResult.Success == false) continue;

                // Notify emitter that file has been consumed
                File.Delete(DEEP_LINK_BRIDGE_PATH);

                Result<DeepLink> deepLinkCreateResult = DeepLink.FromJson(contentResult.Value);

                if (deepLinkCreateResult.Success)
                {
                    DeepLink deeplink = deepLinkCreateResult.Value;
                    Result handleResult = handle.HandleDeepLink(deeplink);

                    if (handleResult.Success)
                        ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, $"{handle.Name} successfully handled deeplink: {deeplink}");
                    else
                        ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, $"{handle.Name} raised error on handle deeplink: {deeplink}, error {handleResult.ErrorMessage}");
                }
                else
                    ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, $"Cannot deserialize deeplink content: {deepLinkCreateResult.ErrorMessage}");
            }
        }
    }
}
