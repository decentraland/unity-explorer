using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PrivateWorlds.UI;
using MVC;
using System;
using System.Threading;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Implements IWorldAccessGate: runs permission check, shows popup when needed,
    /// validates password with retry loop.
    /// </summary>
    public class PrivateWorldAccessHandler : IWorldAccessGate
    {
        private const int MAX_PASSWORD_ATTEMPTS = 3;

        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IMVCManager mvcManager;
        private Action? beforePopupShown;

        public PrivateWorldAccessHandler(
            IWorldPermissionsService worldPermissionsService,
            IMVCManager mvcManager)
        {
            this.worldPermissionsService = worldPermissionsService;
            this.mvcManager = mvcManager;
        }

        /// <summary>
        /// Optional callback invoked before showing the password popup (e.g. to minimize chat).
        /// Set by the plugin that has access to the chat event bus.
        /// </summary>
        public void SetBeforePopupCallback(Action? callback) => beforePopupShown = callback;

        public async UniTask<WorldAccessResult> CheckAccessAsync(string worldName, string? ownerAddress, CancellationToken ct)
        {
            try
            {
                var context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);

                return context.Result switch
                {
                    WorldAccessCheckResult.Allowed => WorldAccessResult.Allowed,
                    WorldAccessCheckResult.CheckFailed => WorldAccessResult.CheckFailed,
                    WorldAccessCheckResult.AccessDenied => await ShowAccessDeniedAsync(worldName, context.AccessInfo?.OwnerAddress ?? ownerAddress, ct),
                    WorldAccessCheckResult.PasswordRequired => await HandlePasswordRequiredAsync(worldName, context.AccessInfo?.OwnerAddress ?? ownerAddress, ct),
                    _ => WorldAccessResult.Allowed,
                };
            }
            catch (OperationCanceledException)
            {
                throw; // Let the caller (RealmNavigator) distinguish timeout vs user cancellation
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.REALM);
                return WorldAccessResult.CheckFailed;
            }
        }

        private async UniTask<WorldAccessResult> ShowAccessDeniedAsync(string worldName, string? ownerAddress, CancellationToken ct)
        {
            try
            {
                var popupParams = new PrivateWorldPopupParams(worldName, PrivateWorldPopupMode.AccessDenied, ownerAddress);
                await mvcManager.ShowAsync(PrivateWorldPopupController.IssueCommand(popupParams), ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.REALM);
            }

            return WorldAccessResult.Denied;
        }

        private async UniTask<WorldAccessResult> HandlePasswordRequiredAsync(string worldName, string? ownerAddress, CancellationToken ct)
        {
            string? errorMessage = null;

            for (int attempt = 0; attempt < MAX_PASSWORD_ATTEMPTS; attempt++)
            {
                var popupParams = new PrivateWorldPopupParams(worldName, PrivateWorldPopupMode.PasswordRequired, ownerAddress)
                {
                    ErrorMessage = errorMessage
                };

                beforePopupShown?.Invoke();
                await mvcManager.ShowAsync(PrivateWorldPopupController.IssueCommand(popupParams), ct);

                if (popupParams.Result == PrivateWorldPopupResult.Cancelled)
                    return WorldAccessResult.PasswordCancelled;

                if (popupParams.Result == PrivateWorldPopupResult.PasswordSubmitted)
                {
                    bool valid = await worldPermissionsService.ValidatePasswordAsync(
                        worldName, popupParams.EnteredPassword ?? string.Empty, ct);

                    if (valid)
                        return WorldAccessResult.Allowed;

                    errorMessage = "Incorrect password. Please try again.";
                }
            }

            return WorldAccessResult.PasswordCancelled;
        }
    }
}
