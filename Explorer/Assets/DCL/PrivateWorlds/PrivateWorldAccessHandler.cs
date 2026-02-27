using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PrivateWorlds.UI;
using ECS;
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
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IMVCManager mvcManager;
        private readonly IRealmData realmData;

        public PrivateWorldAccessHandler(
            IWorldPermissionsService worldPermissionsService,
            IMVCManager mvcManager,
            IRealmData realmData)
        {
            this.worldPermissionsService = worldPermissionsService;
            this.mvcManager = mvcManager;
            this.realmData = realmData;
        }

        public async UniTask<WorldAccessResult> CheckAccessAsync(string worldName, string? ownerAddress, CancellationToken ct)
        {
            ReportHub.Log(ReportCategory.REALM, $"[PrivateWorldAccessHandler] Checking access for '{worldName}'");

            try
            {
                var context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);
                return context.Result switch
                {
                    WorldAccessCheckResult.Allowed => HandleAllowed(),
                    WorldAccessCheckResult.CheckFailed => WorldAccessResult.CheckFailed,
                    WorldAccessCheckResult.AccessDenied => await ShowAccessDeniedAsync(worldName, context.AccessInfo?.OwnerAddress ?? ownerAddress, ct),
                    WorldAccessCheckResult.PasswordRequired => await HandlePasswordRequiredAsync(worldName, context.AccessInfo?.OwnerAddress ?? ownerAddress, ct),
                    _ => WorldAccessResult.CheckFailed,
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

        private WorldAccessResult HandleAllowed()
        {
            realmData.WorldCommsSecret = string.Empty;
            return WorldAccessResult.Allowed;
        }

        private async UniTask<WorldAccessResult> HandlePasswordRequiredAsync(string worldName, string? ownerAddress, CancellationToken ct)
        {
            // Start fresh before prompting for password so this flow always uses a newly entered value.
            realmData.WorldCommsSecret = string.Empty;

            var popupParams = new PrivateWorldPopupParams(worldName, PrivateWorldPopupMode.PasswordRequired, ownerAddress);

            await mvcManager.ShowAsync(PrivateWorldPopupController.IssueCommand(popupParams), ct);

            if (popupParams.Result == PrivateWorldPopupResult.PasswordSubmitted)
            {
                realmData.WorldCommsSecret = popupParams.EnteredPassword ?? string.Empty;
                ReportHub.Log(ReportCategory.REALM, $"[PrivateWorldAccessHandler] Password validated for '{worldName}', secret stored (length={popupParams.EnteredPassword?.Length ?? 0})");
                return WorldAccessResult.Allowed;
            }

            return WorldAccessResult.PasswordCancelled;
        }
    }
}
