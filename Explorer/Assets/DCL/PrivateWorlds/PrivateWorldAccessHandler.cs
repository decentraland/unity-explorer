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
        private readonly IWorldCommsSecret worldCommsSecret;

        public PrivateWorldAccessHandler(
            IWorldPermissionsService worldPermissionsService,
            IMVCManager mvcManager,
            IWorldCommsSecret worldCommsSecret)
        {
            this.worldPermissionsService = worldPermissionsService;
            this.mvcManager = mvcManager;
            this.worldCommsSecret = worldCommsSecret;
        }

        public async UniTask<WorldAccessResult> CheckAccessAsync(string worldName, string? ownerAddress, CancellationToken ct)
        {
            // Clear any secret from a previous world before checking the new one.
            // A new secret is set only if password validation succeeds.
            worldCommsSecret.Secret = null;
            ReportHub.Log(ReportCategory.REALM, $"[PrivateWorldAccessHandler] Checking access for '{worldName}', secret cleared");

            try
            {
                var context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);

                return context.Result switch
                {
                    WorldAccessCheckResult.Allowed => HandleAllowed(context),
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

        private WorldAccessResult HandleAllowed(WorldAccessCheckContext context)
        {
            // Owner of a SharedSecret world still needs a secret in the comms handshake.
            // Backend validates ownership via signed fetch, so the actual value doesn't matter.
            if (context.AccessInfo?.AccessType == WorldAccessType.SharedSecret)
                worldCommsSecret.Secret = "owner";

            return WorldAccessResult.Allowed;
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

                await mvcManager.ShowAsync(PrivateWorldPopupController.IssueCommand(popupParams), ct);

                if (popupParams.Result == PrivateWorldPopupResult.Cancelled)
                    return WorldAccessResult.PasswordCancelled;

                if (popupParams.Result == PrivateWorldPopupResult.PasswordSubmitted)
                {
                    bool valid = await worldPermissionsService.ValidatePasswordAsync(
                        worldName, popupParams.EnteredPassword ?? string.Empty, ct);

                    if (valid)
                    {
                        worldCommsSecret.Secret = popupParams.EnteredPassword;
                        ReportHub.Log(ReportCategory.REALM, $"[PrivateWorldAccessHandler] Password validated for '{worldName}', secret stored (length={popupParams.EnteredPassword?.Length ?? 0})");
                        return WorldAccessResult.Allowed;
                    }

                    errorMessage = "Incorrect password. Please try again.";
                }
            }

            return WorldAccessResult.PasswordCancelled;
        }
    }
}
