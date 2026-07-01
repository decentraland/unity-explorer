using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using System;
using System.IO;
using System.Threading;
using UnityEngine;

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
            Debug.Log($"[DLDBG] Sentinel STARTED . Watching: {DEEP_LINK_BRIDGE_PATH}"); // TODO: temporary deep-link debug log, remove.

            while (token.IsCancellationRequested == false)
            {
                bool cancelled = await UniTask.Delay(CHECK_IN_PERIOD, cancellationToken: token).SuppressCancellationThrow();
                if (cancelled) continue;

                // File.Exists method is lightweight and can be used in this loop
                if (!File.Exists(DEEP_LINK_BRIDGE_PATH)) continue;

                // TODO: temporary deep-link debug log, remove. FileInfo.Exists is checked to avoid throwing if the
                // file vanished between the check above and reading its metadata (launcher rewrite / other instance).
                var bridgeInfo = new FileInfo(DEEP_LINK_BRIDGE_PATH);
                if (bridgeInfo.Exists)
                    Debug.Log($"[DLDBG] Sentinel: bridge file FOUND | created={bridgeInfo.CreationTimeUtc:HH:mm:ss.fff}Z written={bridgeInfo.LastWriteTimeUtc:HH:mm:ss.fff}Z size={bridgeInfo.Length}B path={DEEP_LINK_BRIDGE_PATH}");

                Result<string> contentResult = await File.ReadAllTextAsync(DEEP_LINK_BRIDGE_PATH, token)!.SuppressToResultAsync<string>(ReportCategory.RUNTIME_DEEPLINKS);

                // Transient IO read failure: leave the file for the next check-in.
                if (contentResult.Success == false) continue;

                Debug.Log($"[DLDBG] Sentinel: raw content = {contentResult.Value}"); // TODO: temporary deep-link debug log, remove.

                // Parse before deleting: a corrupt file is dropped, a valid one is handled.
                Result<DeepLink> deepLinkCreateResult = DeepLink.FromJson(contentResult.Value);

                if (deepLinkCreateResult.Success == false)
                {
                    ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, $"Cannot deserialize deeplink content: {deepLinkCreateResult.ErrorMessage}");
                    TryArchiveBridgeFile();
                    continue;
                }

                // A false result means no login flow is awaiting the signin yet: keep the file so it can be picked up later.
                if (handle.HandleDeepLink(deepLinkCreateResult.Value))
                {
                    Debug.Log("[DLDBG] Sentinel: deeplink consumed, archiving bridge file"); // TODO: temporary deep-link debug log, remove.
                    TryArchiveBridgeFile();
                }
                else
                    Debug.Log("[DLDBG] Sentinel: deeplink deferred, keeping bridge file"); // TODO: temporary deep-link debug log, remove.
            }
        }

        // TODO: temporary deep-link debug. Instead of deleting, rename the consumed bridge file with the consuming
        // process PID so the traces stay on disk for inspection. Restore File.Delete once the flow is understood.
        private static void TryArchiveBridgeFile()
        {
            try
            {
                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                string directory = Path.GetDirectoryName(DEEP_LINK_BRIDGE_PATH)!;
                string archivedPath = Path.Combine(directory, $"deeplink-bridge.consumed.pid{pid}.{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");

                File.Move(DEEP_LINK_BRIDGE_PATH, archivedPath);
                Debug.Log($"[DLDBG] Sentinel: archived bridge file -> {archivedPath}"); // TODO: temporary deep-link debug log, remove.
            }
            catch (Exception e)
            {
                // Rename can fail transiently (file locked/rewritten by the launcher). Log and keep the loop alive.
                ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, $"Failed to archive deeplink bridge file: {e.Message}");
            }
        }
    }
}
