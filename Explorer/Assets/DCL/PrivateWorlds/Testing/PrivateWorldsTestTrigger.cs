using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.PrivateWorlds;
using DCL.PrivateWorlds.UI;
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

        [Header("Test Configuration")]
        [SerializeField] private string testWorldName = "yourname.dcl.eth";
        [SerializeField] private string testWrongPassword = "wrong";
        [SerializeField] private string testCorrectPassword = "abc123";

        private IWorldPermissionsService? permissionsService;
        private IMVCManager? mvcManager;
        private IWorldAccessGate? worldAccessGate;
        private CancellationTokenSource? cts;

        /// <summary>
        /// Initialize the test trigger with required services.
        /// </summary>
        public void Initialize(IWorldPermissionsService permissionsService, IMVCManager mvcManager, IWorldAccessGate worldAccessGate)
        {
            this.permissionsService = permissionsService;
            this.mvcManager = mvcManager;
            this.worldAccessGate = worldAccessGate;
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
                bool valid = await permissionsService!.ValidatePasswordAsync(testWorldName, password ?? string.Empty, ct);
                ReportHub.Log(ReportCategory.REALM, $"[ValidatePassword] Result: {(valid ? "valid" : "invalid")}");
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
    }
}
