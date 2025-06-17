using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
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

        [Serializable]
        private struct DeepLinkDTO
        {
            public string deeplink;
        }

        /// <summary>
        /// Runs for the lifetime of the app.
        /// </summary>
        public static async UniTaskVoid StartListenForDeepLinksAsync<T>(T handles, CancellationToken token) where T: IEnumerable<IDeepLinkHandle>
        {
            while (token.IsCancellationRequested == false)
            {
                await UniTask.Delay(CHECK_IN_PERIOD, cancellationToken: token);

                // File.Exists method is lightweight and can be used in this loop
                if (!File.Exists(DEEP_LINK_BRIDGE_PATH)) continue;

                string content = await File.ReadAllTextAsync(DEEP_LINK_BRIDGE_PATH, token)!;

                // Notify emitter that file has been consumed
                File.Delete(DEEP_LINK_BRIDGE_PATH);

                DeepLinkDTO dto = JsonUtility.FromJson<DeepLinkDTO>(content);

                if (dto.deeplink == null)
                {
                    ReportHub.LogError(ReportCategory.RUNTIME_DEEPLINKS, $"Cannot deserialize deeplink content: {content}");
                    continue;
                }

                foreach (IDeepLinkHandle deepLinkHandle in handles)
                {
                    HandleResult result = deepLinkHandle.HandleDeepLink(dto.deeplink);

                    result.Match(
                        deepLinkHandle,
                        onHandleError: static (handle, error) => ReportHub.LogError(
                            ReportCategory.RUNTIME_DEEPLINKS,
                            $"DeepLinkHandle '{handle.Name}' failed to handle deeplink with error message: {error.Message}"
                        ),
                        onOk: static _ =>
                        {
                            /* ignore */
                        }
                    );
                }
            }
        }
    }
}
