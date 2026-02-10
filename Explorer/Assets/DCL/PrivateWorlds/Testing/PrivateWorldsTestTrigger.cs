using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PrivateWorlds;
using DCL.PrivateWorlds.UI;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PrivateWorlds.Testing
{
    /// <summary>
    /// Debug test controller for Private Worlds feature.
    /// Use context menu actions to test the popup UI without RealmNavigator integration.
    /// Spawn via DynamicWorldContainer in Editor mode.
    /// </summary>
    public class PrivateWorldsTestTrigger : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private string testWorldName = "yourname.dcl.eth";
        [SerializeField] private string testWrongPassword = "wrong";
        [SerializeField] private string testCorrectPassword = "abc123";

        private IWorldPermissionsService? permissionsService;
        private IMVCManager? mvcManager;
        private IEventBus? eventBus;
        private CancellationTokenSource? cts;

        /// <summary>
        /// Initialize the test trigger with required services.
        /// Call this from DynamicWorldContainer after instantiating the component.
        /// </summary>
        public void Initialize(IWorldPermissionsService permissionsService, IMVCManager mvcManager, IEventBus eventBus)
        {
            this.permissionsService = permissionsService;
            this.mvcManager = mvcManager;
            this.eventBus = eventBus;
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

        [ContextMenu("Test - Check World Access")]
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

        [ContextMenu("Test - Full Permission Flow (Simulate Join)")]
        public void TestFullPermissionFlow()
        {
            if (permissionsService == null || mvcManager == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: Services not initialized. Call Initialize() first.");
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            SimulateFullPermissionFlowAsync(cts.Token).Forget();
        }

        private async UniTaskVoid SimulateFullPermissionFlowAsync(CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, $"Simulating full permission flow for world: {testWorldName}");

                // Step 1: Check access
                var accessContext = await permissionsService!.CheckWorldAccessAsync(testWorldName, ct);
                var accessInfo = accessContext.AccessInfo;

                ReportHub.Log(ReportCategory.REALM, $"Access check completed. Result: {accessContext.Result}, Type: {accessInfo?.AccessType.ToString() ?? "N/A"}");

                if (accessContext.Result == WorldAccessCheckResult.Allowed)
                {
                    ReportHub.Log(ReportCategory.REALM, "Access granted! Would proceed to load world.");
                    return;
                }

                if (accessContext.Result == WorldAccessCheckResult.CheckFailed)
                {
                    ReportHub.Log(ReportCategory.REALM, "Permission check failed. Would fallback to server-side check.");
                    return;
                }

                string? ownerAddress = accessInfo?.OwnerAddress;

                // Step 2: Handle based on result
                switch (accessContext.Result)
                {
                    case WorldAccessCheckResult.AccessDenied:
                        ReportHub.Log(ReportCategory.REALM, "User not on allow list. Showing access denied popup...");

                        var deniedParams = new PrivateWorldPopupParams(
                            testWorldName,
                            PrivateWorldPopupMode.AccessDenied,
                            ownerAddress
                        );

                        await mvcManager!.ShowAsync(PrivateWorldPopupController.IssueCommand(deniedParams), ct);

                        ReportHub.Log(ReportCategory.REALM, "Access denied flow completed.");
                        break;

                    case WorldAccessCheckResult.PasswordRequired:
                        ReportHub.Log(ReportCategory.REALM, "World requires password. Showing password popup...");

                        var passwordParams = new PrivateWorldPopupParams(
                            testWorldName,
                            PrivateWorldPopupMode.PasswordRequired,
                            ownerAddress
                        );

                        await mvcManager!.ShowAsync(PrivateWorldPopupController.IssueCommand(passwordParams), ct);

                        if (passwordParams.Result == PrivateWorldPopupResult.PasswordSubmitted)
                            ReportHub.Log(ReportCategory.REALM, "Password submitted! Would proceed to validate and load world.");
                        else
                            ReportHub.Log(ReportCategory.REALM, "Password entry cancelled.");
                        break;

                    default:
                        ReportHub.Log(ReportCategory.REALM, "Unexpected access result - world should be accessible.");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.REALM, "Permission flow cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"Error in permission flow: {ex.Message}");
            }
        }

        // ============================================================
        // Event Bus Testing (simulates what RealmNavigator does)
        // ============================================================

        [ContextMenu("Test - EventBus: Check World Access")]
        public void TestEventBusCheckWorldAccess()
        {
            if (eventBus == null)
            {
                ReportHub.LogError(ReportCategory.REALM, "PrivateWorldsTestTrigger: EventBus not initialized.");
                return;
            }

            cts?.Cancel();
            cts?.Dispose();
            cts = new CancellationTokenSource();

            TestEventBusCheckWorldAccessAsync(cts.Token).Forget();
        }

        private async UniTaskVoid TestEventBusCheckWorldAccessAsync(CancellationToken ct)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, "[EventBus] Publishing CheckWorldAccessEvent...");

                var resultSource = new UniTaskCompletionSource<WorldAccessResult>();
                var evt = new CheckWorldAccessEvent(testWorldName, null, resultSource);
                eventBus!.Publish(evt);

                ReportHub.Log(ReportCategory.REALM, "[EventBus] Waiting for result...");
                var result = await resultSource.Task.AttachExternalCancellation(ct);

                ReportHub.Log(ReportCategory.REALM, $"[EventBus] World access result: {result}");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.REALM, "[EventBus] Check world access cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.LogError(ReportCategory.REALM, $"[EventBus] Error: {ex.Message}");
            }
        }
    }
}
