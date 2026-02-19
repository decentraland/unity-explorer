using Cysharp.Threading.Tasks;
using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PrivateWorlds;
using DCL.PrivateWorlds.UI;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PrivateWorlds.Testing
{
    /// <summary>
    /// Debug test controller for Private Worlds feature.
    /// Use context menu actions to test the popup UI and access gate without RealmNavigator integration.
    /// Spawn via PrivateWorldsPlugin in Editor mode.
    /// </summary>
    public class PrivateWorldsTestTrigger : MonoBehaviour
    {
        private const string PERMISSION_CHECK_FAILED_MESSAGE =
            "Could not verify world permissions. You may experience access issues.";
        private const string PROBE_REPORT_CATEGORY = ReportCategory.MULTIPLAYER;

        private const string MISSING_SECRET_FALLBACK = "__missing_secret__";

        [Header("Test Configuration")]
        [SerializeField] private string testWorldName = "yourname.dcl.eth";
        [Tooltip("Scene entity ID (IPFS hash from COMMS_SCENE_HANDLER logs, e.g. bafkreib6uef7drqguiafjani5lrau37v6iilwo2nwuls7xqzndobp2lznq for blackhole.dcl.eth)")]
        [SerializeField] private string testSceneId = "bafkreib6uef7drqguiafjani5lrau37v6iilwo2nwuls7xqzndobp2lznq";
        [SerializeField] private string testRoomId = "world-dev-yourname.dcl.eth";
        [SerializeField] private string testWrongPassword = "wrong";
        [SerializeField] private string testCorrectPassword = "abc123";
        [SerializeField] private string testAdapterToCast = "";

        private IWorldPermissionsService? permissionsService;
        private IMVCManager? mvcManager;
        private IWorldAccessGate? worldAccessGate;
        private IWebRequestController? webRequestController;
        private IDecentralandUrlsSource? urlsSource;
        private CancellationTokenSource? cts;

        /// <summary>
        /// Initialize the test trigger with required services.
        /// </summary>
        public void Initialize(
            IWorldPermissionsService permissionsService,
            IMVCManager mvcManager,
            IWorldAccessGate worldAccessGate,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.permissionsService = permissionsService;
            this.mvcManager = mvcManager;
            this.worldAccessGate = worldAccessGate;
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            ReportHub.Log(ReportCategory.REALM, "PrivateWorldsTestTrigger initialized");
        }

        private void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        [ContextMenu("Test - Fetch World Permissions")]
        public void TestFetchWorldPermissions()
        {
            if (permissionsService == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: Service not initialized. Call Initialize() first.");
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            FetchWorldPermissionsAsync(cts.Token).Forget();
        }

        private async UniTaskVoid FetchWorldPermissionsAsync(CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, $"Fetching permissions for world: {testWorldName}");

                var permissions = await permissionsService!.GetWorldPermissionsAsync(testWorldName, ct);

                if (permissions != null)
                {
                    ReportHub.Log(ReportCategory.REALM, $"World Permissions Result:\n" +
                        $"  AccessType: {permissions.AccessType}\n" +
                        $"  OwnerAddress: {permissions.OwnerAddress ?? "N/A"}\n" +
                        $"  RequiresPassword: {permissions.AccessType == WorldAccessType.SharedSecret}\n" +
                        $"  AllowedWallets Count: {permissions.AllowedWallets.Count}\n" +
                        $"  AllowedCommunities Count: {permissions.AllowedCommunities.Count}");
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.REALM, $"No permissions found for world: {testWorldName}");
                }
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.REALM, "Permission fetch cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"Error fetching permissions: {ex.Message}");
            }
        }

        [ContextMenu("Test - Check World Access (Raw)")]
        public void TestCheckWorldAccess()
        {
            if (permissionsService == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: Service not initialized. Call Initialize() first.");
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            CheckWorldAccessAsync(cts.Token).Forget();
        }

        private async UniTaskVoid CheckWorldAccessAsync(CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, $"Checking access for world: {testWorldName}");

                var accessContext = await permissionsService!.CheckWorldAccessAsync(testWorldName, ct);

                ReportHub.Log(ReportCategory.REALM, $"Access Check Result:\n" +
                    $"  Result: {accessContext.Result}\n" +
                    $"  AccessType: {accessContext.AccessInfo?.AccessType.ToString() ?? "N/A"}\n" +
                    $"  OwnerAddress: {accessContext.AccessInfo?.OwnerAddress ?? "N/A"}\n" +
                    $"  ErrorMessage: {accessContext.ErrorMessage ?? "N/A"}");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.REALM, "Access check cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"Error checking access: {ex.Message}");
            }
        }

        [ContextMenu("Test - Validate Password (Wrong)")]
        public void TestValidatePasswordWrong()
        {
            if (permissionsService == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: Service not initialized. Call Initialize() first.");
                return;
            }
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
            ValidatePasswordAsync(testWrongPassword, cts.Token).Forget();
        }

        [ContextMenu("Test - Validate Password (Correct)")]
        public void TestValidatePasswordCorrect()
        {
            if (permissionsService == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: Service not initialized. Call Initialize() first.");
                return;
            }
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
            ValidatePasswordAsync(testCorrectPassword, cts.Token).Forget();
        }

        private async UniTaskVoid ValidatePasswordAsync(string password, CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, $"[ValidatePassword] World: {testWorldName}, password length: {password?.Length ?? 0}");
                ValidatePasswordResult result = await permissionsService!.ValidatePasswordAsync(testWorldName, password ?? string.Empty, ct);
                ReportHub.Log(ReportCategory.REALM, $"[ValidatePassword] Result: {(result.Success ? "valid" : "invalid")}{(result.ErrorMessage != null ? $", error: {result.ErrorMessage}" : "")}");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.REALM, "[ValidatePassword] Cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"[ValidatePassword] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [ContextMenu("Test - Show Password Required Popup")]
        public void TestShowPasswordRequiredPopup()
        {
            if (mvcManager == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: MVC Manager not initialized. Call Initialize() first.");
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            ShowPasswordPopupAsync(cts.Token).Forget();
        }

        private async UniTaskVoid ShowPasswordPopupAsync(CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, "Showing password required popup...");

                var popupParams = new PrivateWorldPopupParams(
                    testWorldName,
                    PrivateWorldPopupMode.PasswordRequired,
                    "0x1234567890abcdef1234567890abcdef12345678"
                );

                await mvcManager!.ShowAsync(PrivateWorldPopupController.IssueCommand(popupParams), ct);

                ReportHub.Log(ReportCategory.REALM, $"Password Popup Result:\n" +
                    $"  Result: {popupParams.Result}\n" +
                    $"  Password: {(string.IsNullOrEmpty(popupParams.EnteredPassword) ? "(empty)" : "(provided)")}");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.REALM, "Password popup cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"Error showing password popup: {ex.Message}");
            }
        }

        [ContextMenu("Test - Show Access Denied Popup")]
        public void TestShowAccessDeniedPopup()
        {
            if (mvcManager == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: MVC Manager not initialized. Call Initialize() first.");
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            ShowAccessDeniedPopupAsync(cts.Token).Forget();
        }

        private async UniTaskVoid ShowAccessDeniedPopupAsync(CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, "Showing access denied popup...");

                var popupParams = new PrivateWorldPopupParams(
                    testWorldName,
                    PrivateWorldPopupMode.AccessDenied,
                    "0x1234567890abcdef1234567890abcdef12345678"
                );

                await mvcManager!.ShowAsync(PrivateWorldPopupController.IssueCommand(popupParams), ct);

                ReportHub.Log(ReportCategory.REALM, $"Access Denied Popup Result: {popupParams.Result}");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.REALM, "Access denied popup cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"Error showing access denied popup: {ex.Message}");
            }
        }

        [ContextMenu("Test - Cancel During Popup")]
        public void TestCancelDuringPopup()
        {
            if (mvcManager == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: MVC Manager not initialized. Call Initialize() first.");
                return;
            }
            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();
            ShowPasswordPopupThenCancelAfterDelayAsync(cts, TimeSpan.FromSeconds(3)).Forget();
        }

        private async UniTaskVoid ShowPasswordPopupThenCancelAfterDelayAsync(CancellationTokenSource popupCts, TimeSpan cancelAfter)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, "Showing password popup; will cancel after 3 seconds...");
                ShowPasswordPopupAsync(popupCts.Token).Forget();
                await UniTask.Delay(cancelAfter);
                popupCts.Cancel();
                ReportHub.Log(ReportCategory.REALM, "Cancel during popup completed. Verify popup closed cleanly.");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"Cancel during popup error: {ex.Message}");
            }
        }

        [ContextMenu("Test - Gate: Full Access Flow")]
        public void TestGateFullAccessFlow()
        {
            if (worldAccessGate == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: WorldAccessGate not initialized.");
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            GateFullAccessFlowAsync(cts.Token).Forget();
        }

        private async UniTaskVoid GateFullAccessFlowAsync(CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, $"[Gate] Calling CheckAccessAsync for world: {testWorldName}");
                var result = await worldAccessGate!.CheckAccessAsync(testWorldName, null, ct);
                ReportHub.Log(ReportCategory.REALM, $"[Gate] World access result: {result}");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.REALM, "[Gate] Access flow cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"[Gate] Error: {ex.Message}");
            }
        }

        [ContextMenu("Test - Simulate CheckFailed Toast")]
        public void TestSimulateCheckFailedToast()
        {
            if (NotificationsBusController.Instance == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: NotificationsBusController not initialized.");
                return;
            }
            NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(PERMISSION_CHECK_FAILED_MESSAGE));
            ReportHub.Log(ReportCategory.REALM, "CheckFailed toast triggered. Verify the toast appears.");
        }

        [ContextMenu("Test - World Comms")]
        public void TestWorldCommsEndpoint()
        {
            if (!ValidateWorldServerDependencies())
                return;

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            string metadata = BuildHandshakeMetadata(testCorrectPassword);
            string url = string.Format(urlsSource!.Url(DecentralandUrl.WorldComms), testWorldName);
            ProbeSignedEndpointAsync(url, metadata, "WorldComms", cts.Token).Forget();
        }

        [ContextMenu("Test - World Scene Comms")]
        public void TestWorldSceneCommsEndpoint()
        {
            if (!ValidateWorldServerDependencies())
                return;

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            string metadata = BuildHandshakeMetadata(testCorrectPassword);
            string url = string.Format(urlsSource!.Url(DecentralandUrl.WorldCommsSceneAdapter), testWorldName, testSceneId);
            ProbeSignedEndpointAsync(url, metadata, "WorldSceneComms", cts.Token).Forget();
        }

        [ContextMenu("Test - World Scene Comms (No Secret)")]
        public void TestWorldSceneCommsNoSecretEndpoint()
        {
            if (!ValidateWorldServerDependencies())
                return;

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            string metadata = BuildHandshakeMetadata(MISSING_SECRET_FALLBACK);
            string url = string.Format(urlsSource!.Url(DecentralandUrl.WorldCommsSceneAdapter), testWorldName, testSceneId);
            ProbeSignedEndpointAsync(url, metadata, "WorldSceneCommsNoSecret", cts.Token).Forget();
        }

        [ContextMenu("Test - Get Comms Adapter {roomId}")]
        public void TestGetCommsAdapterByRoomIdEndpoint()
        {
            if (!ValidateWorldServerDependencies())
                return;

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            string metadata = BuildHandshakeMetadata(string.Empty);
            string url = $"{GetWorldContentServerBaseUrl()}/get-comms-adapter/{testRoomId}";
            ProbeSignedEndpointAsync(url, metadata, "GetCommsAdapterByRoomId", cts.Token).Forget();
        }

        [ContextMenu("Test - Cast Adapter {roomId}")]
        public void TestCastAdapterByRoomIdEndpoint()
        {
            if (!ValidateWorldServerDependencies())
                return;

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            CastAdapterByRoomIdAsync(cts.Token).Forget();
        }

        private async UniTaskVoid CastAdapterByRoomIdAsync(CancellationToken ct)
        {
            try
            {
                string url = $"{GetWorldContentServerBaseUrl()}/cast-adapter/{testRoomId}";
                string metadata = BuildHandshakeMetadata(string.Empty);
                string payload = string.IsNullOrWhiteSpace(testAdapterToCast)
                                     ? "{}"
                                     : $"{{\"adapter\":\"{EscapeJsonString(testAdapterToCast)}\"}}";

                ReportHub.Log(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:CastAdapterByRoomId] POST {url}");
                ReportHub.Log(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:CastAdapterByRoomId] metadata={metadata}");
                ReportHub.Log(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:CastAdapterByRoomId] payload={payload}");

                var request = webRequestController!
                             .SignedFetchPostAsync(
                                  new CommonArguments(URLAddress.FromString(url), RetryPolicy.NONE, timeout: 30),
                                  GenericPostArguments.CreateJson(payload),
                                  metadata,
                                  ct);

                string body = await request.StoreTextAsync();
                ReportHub.Log(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:CastAdapterByRoomId] success, body={body}");
            }
            catch (UnityWebRequestException e)
            {
                ReportHub.LogError(PROBE_REPORT_CATEGORY,
                    $"[WorldCommsProbe:CastAdapterByRoomId] status={e.ResponseCode}, error={e.Message}, body={e.Text}");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(PROBE_REPORT_CATEGORY, "[WorldCommsProbe:CastAdapterByRoomId] cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:CastAdapterByRoomId] unexpected error: {ex.Message}");
            }
        }

        private async UniTaskVoid ProbeSignedEndpointAsync(string url, string metadata, string probeName, CancellationToken ct)
        {
            try
            {
                ReportHub.Log(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:{probeName}] POST {url}");
                ReportHub.Log(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:{probeName}] body={metadata}");
                ReportHub.Log(PROBE_REPORT_CATEGORY,
                    "[WorldCommsProbe] For Postman: Method=POST, use the URL and body above. Copy the x-identity-auth-chain-0, x-identity-auth-chain-1, x-identity-auth-chain-2 headers from the GENERIC_WEB_REQUEST log lines emitted when this request is sent.");

                var request = webRequestController!
                             .SignedFetchPostAsync(
                                  new CommonArguments(URLAddress.FromString(url), RetryPolicy.NONE, timeout: 30),
                                  metadata,
                                  ct);

                string body = await request.StoreTextAsync();
                ReportHub.Log(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:{probeName}] success, body={body}");
            }
            catch (UnityWebRequestException e)
            {
                ReportHub.LogError(PROBE_REPORT_CATEGORY,
                    $"[WorldCommsProbe:{probeName}] status={e.ResponseCode}, error={e.Message}, body={e.Text}");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:{probeName}] cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(PROBE_REPORT_CATEGORY, $"[WorldCommsProbe:{probeName}] unexpected error: {ex.Message}");
            }
        }

        private bool ValidateWorldServerDependencies()
        {
            if (webRequestController == null || urlsSource == null)
            {
                ReportHub.LogError(PROBE_REPORT_CATEGORY,
                    "PrivateWorldsTestTrigger: WebRequestController/UrlsSource not initialized. Call Initialize() first.");
                return false;
            }

            return true;
        }

        private string GetWorldContentServerBaseUrl()
        {
            string worldCommsTemplate = urlsSource!.Url(DecentralandUrl.WorldComms);
            string suffix = "/worlds/{0}/comms";

            int index = worldCommsTemplate.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                return worldCommsTemplate.Substring(0, index);

            // Fallback for unexpected template formats
            return worldCommsTemplate.Replace("/worlds/{0}/comms", string.Empty).TrimEnd('/');
        }

        private static string BuildHandshakeMetadata(string passwordOrEmpty)
        {
            if (string.IsNullOrEmpty(passwordOrEmpty))
                return "{\"intent\":\"dcl:explorer:comms-handshake\",\"signer\":\"dcl:explorer\",\"isGuest\":false}";

            return
                $"{{\"intent\":\"dcl:explorer:comms-handshake\",\"signer\":\"dcl:explorer\",\"isGuest\":false,\"secret\":\"{EscapeJsonString(passwordOrEmpty)}\"}}";
        }

        private static string EscapeJsonString(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
