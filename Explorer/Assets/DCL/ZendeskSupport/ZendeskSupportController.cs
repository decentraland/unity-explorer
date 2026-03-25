using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;

#if ZENDESK_ENABLED
using Zendesk.Runtime.SDK;
#endif

namespace DCL.ZendeskSupport
{
    /// <summary>
    /// Controls the Zendesk Support SDK integration.
    /// Handles initialization and exposes hooks to show the Messaging and Home UIs.
    ///
    /// Usage:
    ///   1. Call InitializeAsync() once at startup (e.g. from SidebarPlugin).
    ///   2. Call ShowMessagingAsync() to open the live-chat / ticketing UI.
    ///   3. Call ShowHomeAsync() to open the Help Center home screen.
    ///
    /// The ZENDESK_ENABLED scripting define is set via Project Settings → Player → Scripting
    /// Define Symbols once the Zendesk UPM package is installed. Without it the class
    /// compiles cleanly but all methods are no-ops, which lets the rest of the project
    /// build while the package is not yet present.
    /// </summary>
    public class ZendeskSupportController : IDisposable
    {
        // -----------------------------------------------------------------------
        // Replace this with the real channel key obtained from your Zendesk
        // Admin Center: https://support.zendesk.com/hc/en-us/articles/1260801714930
        // -----------------------------------------------------------------------
        private const string ZENDESK_CHANNEL_KEY = "YOUR_ZENDESK_CHANNEL_KEY";

        private bool isInitialized;
        private bool isInitializing;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        /// <summary>
        /// Initializes the Zendesk SDK asynchronously.
        /// Safe to call multiple times – subsequent calls are no-ops.
        /// </summary>
        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
            if (isInitialized || isInitializing) return;
            isInitializing = true;

#if ZENDESK_ENABLED
            try
            {
                await ZendeskSdk.InitializeAsync(config =>
                {
                    config.ChannelId = ZENDESK_CHANNEL_KEY;
                    // Optional: force a specific language instead of device default.
                    // config.Language = ZendeskLanguage.English;
                });

                isInitialized = true;
                ReportHub.Log(ReportCategory.UI, "Zendesk Support SDK initialized successfully.");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
            }
            finally
            {
                isInitializing = false;
            }
#else
            await UniTask.CompletedTask;
            isInitializing = false;
            ReportHub.Log(ReportCategory.UI, "Zendesk Support SDK skipped – ZENDESK_ENABLED define not set.");
#endif
        }

        // -----------------------------------------------------------------------
        // UI hooks
        // -----------------------------------------------------------------------

        /// <summary>
        /// Opens the Zendesk Messaging UI (live chat / ticket creation).
        /// Initializes the SDK first if not already done.
        /// </summary>
        public async UniTaskVoid ShowMessagingAsync(CancellationToken ct = default)
        {
#if ZENDESK_ENABLED
            try
            {
                if (!isInitialized)
                    await InitializeAsync(ct);

                if (!isInitialized) return;

                await ZendeskSdk.Instance.Messaging.ShowMessagingAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
            }
#else
            await UniTask.CompletedTask;
            ReportHub.Log(ReportCategory.UI, "[Zendesk] ShowMessagingAsync called (SDK not installed).");
#endif
        }

        /// <summary>
        /// Opens the Zendesk Help Center home screen.
        /// Requires the Guide add-on to be enabled in your Zendesk account.
        /// Initializes the SDK first if not already done.
        /// </summary>
        public async UniTaskVoid ShowHomeAsync(CancellationToken ct = default)
        {
#if ZENDESK_ENABLED
            try
            {
                if (!isInitialized)
                    await InitializeAsync(ct);

                if (!isInitialized) return;

                await ZendeskSdk.Instance.Home.ShowHomeAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
            }
#else
            await UniTask.CompletedTask;
            ReportHub.Log(ReportCategory.UI, "[Zendesk] ShowHomeAsync called (SDK not installed).");
#endif
        }

        /// <summary>
        /// Opens the Zendesk Article Viewer for a specific help-center article URL.
        /// </summary>
        public async UniTaskVoid ShowArticleAsync(string articleUrl, CancellationToken ct = default)
        {
#if ZENDESK_ENABLED
            try
            {
                if (!isInitialized)
                    await InitializeAsync(ct);

                if (!isInitialized) return;

                await ZendeskSdk.Instance.Home.ShowArticleAsync(articleUrl);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.UI);
            }
#else
            await UniTask.CompletedTask;
            ReportHub.Log(ReportCategory.UI, $"[Zendesk] ShowArticleAsync({articleUrl}) called (SDK not installed).");
#endif
        }

        public void Dispose()
        {
            // No-op for now. Add teardown if the SDK exposes a shutdown method.
        }
    }
}
