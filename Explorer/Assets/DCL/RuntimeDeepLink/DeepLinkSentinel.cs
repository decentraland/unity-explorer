using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DCL.RuntimeDeepLink
{
    public static class DeepLinkSentinel
    {
        private static readonly TimeSpan CHECK_IN_PERIOD = TimeSpan.FromMilliseconds(200);

        // Maximum time a deferred signin bridge file is retained on disk; without this cap it would be re-read on every check-in forever.
        private static readonly TimeSpan DEFERRED_SIGNIN_LIFETIME = TimeSpan.FromSeconds(300);

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
            // Measures how long the current file has sat deferred (a signin nobody is logging in for yet),
            // so it can be dropped once DEFERRED_SIGNIN_LIFETIME elapses instead of re-read indefinitely.
            var deferralTimer = new Stopwatch();

            while (token.IsCancellationRequested == false)
            {
                bool cancelled = await UniTask.Delay(CHECK_IN_PERIOD, cancellationToken: token).SuppressCancellationThrow();
                if (cancelled) continue;

                // File.Exists method is lightweight and can be used in this loop
                if (!File.Exists(DEEP_LINK_BRIDGE_PATH))
                {
                    deferralTimer.Reset();
                    continue;
                }

                Result<string> contentResult = await File.ReadAllTextAsync(DEEP_LINK_BRIDGE_PATH, token)!.SuppressToResultAsync<string>(ReportCategory.RUNTIME_DEEPLINKS);

                // Transient IO read failure: leave the file for the next check-in.
                if (!contentResult.Success) continue;

                // Parse before deleting: a corrupt file is dropped, a valid one is handled.
                Result<DeepLink> deepLinkCreateResult = DeepLink.FromJson(contentResult.Value);

                if (deepLinkCreateResult.Success == false)
                {
                    ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, $"Cannot deserialize deeplink content: {deepLinkCreateResult.ErrorMessage}");
                    TryDeleteBridgeFile();
                    deferralTimer.Reset();
                    continue;
                }

                DeepLinkHandleResult result = handle.HandleDeepLink(deepLinkCreateResult.Value);

                if (result == DeepLinkHandleResult.Deferred)
                {
                    // Leave the file in place so the awaiting login can claim it.
                    if (!deferralTimer.IsRunning)
                        deferralTimer.Restart();

                    if (deferralTimer.Elapsed < DEFERRED_SIGNIN_LIFETIME)
                        continue;

                    ReportHub.LogWarning(ReportCategory.RUNTIME_DEEPLINKS, $"no login claimed the signin deeplink within {DEFERRED_SIGNIN_LIFETIME.TotalSeconds:0}s, dropping it");
                    TryDeleteBridgeFile();
                    deferralTimer.Reset();
                    continue;
                }

                deferralTimer.Reset();

                switch (result)
                {
                    case DeepLinkHandleResult.Consumed:
                        ReportHub.Log(ReportCategory.RUNTIME_DEEPLINKS, "successfully handled deeplink");
                        break;
                    case DeepLinkHandleResult.NoMatches:
                        ReportHub.LogWarning(ReportCategory.RUNTIME_DEEPLINKS, "found no actionable content in deeplink");
                        break;
                }

                // Unmatched links are dropped as well: keeping the file would re-read it on every check-in.
                TryDeleteBridgeFile();
            }
        }

        private static void TryDeleteBridgeFile()
        {
            try
            {
                File.Delete(DEEP_LINK_BRIDGE_PATH);
            }
            catch (Exception e)
            {
                // Delete can fail transiently (file locked/rewritten by the launcher). Log and keep the loop alive.
                ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, $"Failed to delete deeplink bridge file: {e.Message}");
            }
        }
    }
}
