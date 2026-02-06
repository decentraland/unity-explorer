using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PrivateWorlds.UI;
using MVC;
using System;
using System.Threading;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Handles CheckWorldAccessEvent: runs permission check, shows popup when needed, validates password with retry loop.
    /// </summary>
    public class PrivateWorldAccessHandler
    {
        private const int MAX_PASSWORD_ATTEMPTS = 3;

        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IMVCManager mvcManager;

        public PrivateWorldAccessHandler(
            IWorldPermissionsService worldPermissionsService,
            IMVCManager mvcManager)
        {
            this.worldPermissionsService = worldPermissionsService;
            this.mvcManager = mvcManager;
        }

        public void OnCheckWorldAccess(CheckWorldAccessEvent evt) => 
            HandleCheckWorldAccessAsync(evt).Forget();

        private async UniTaskVoid HandleCheckWorldAccessAsync(CheckWorldAccessEvent evt)
        {
            try
            {
                ReportHub.Log(ReportCategory.REALM, $"[PrivateWorldAccessHandler] CheckWorldAccessEvent received for world: {evt.WorldName}");
                var context = await worldPermissionsService.CheckWorldAccessAsync(evt.WorldName, CancellationToken.None);
                ReportHub.Log(ReportCategory.REALM, $"[PrivateWorldAccessHandler] Check result: {context.Result} (world: {evt.WorldName})");

                switch (context.Result)
                {
                    case WorldAccessCheckResult.Allowed:
                        ReportHub.Log(ReportCategory.REALM, "[PrivateWorldAccessHandler] Access allowed.");
                        evt.ResultSource.TrySetResult(WorldAccessResult.Allowed);
                        return;

                    case WorldAccessCheckResult.CheckFailed:
                        ReportHub.Log(ReportCategory.REALM, "[PrivateWorldAccessHandler] Check failed, returning CheckFailed.");
                        evt.ResultSource.TrySetResult(WorldAccessResult.CheckFailed);
                        return;

                    case WorldAccessCheckResult.AccessDenied:
                        ReportHub.Log(ReportCategory.REALM, "[PrivateWorldAccessHandler] Access denied (invitation only). Showing popup.");
                        await ShowAccessDeniedAndSetResultAsync(evt, context.AccessInfo?.OwnerAddress);
                        return;

                    case WorldAccessCheckResult.PasswordRequired:
                        ReportHub.Log(ReportCategory.REALM, "[PrivateWorldAccessHandler] Password required. Showing password popup.");
                        await HandlePasswordRequiredAsync(evt, context.AccessInfo?.OwnerAddress);
                        return;

                    default:
                        evt.ResultSource.TrySetResult(WorldAccessResult.Allowed);
                        return;
                }
            }
            catch (OperationCanceledException)
            {
                evt.ResultSource.TrySetResult(WorldAccessResult.PasswordCancelled);
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.REALM);
                evt.ResultSource.TrySetResult(WorldAccessResult.CheckFailed);
            }
        }

        private async UniTask ShowAccessDeniedAndSetResultAsync(CheckWorldAccessEvent evt, string? ownerAddress)
        {
            try
            {
                var popupParams = new PrivateWorldPopupParams(evt.WorldName, PrivateWorldPopupMode.AccessDenied, ownerAddress ?? evt.OwnerAddress);
                    await mvcManager.ShowAsync(PrivateWorldPopupController.IssueCommand(popupParams));
                ReportHub.Log(ReportCategory.REALM, "[PrivateWorldAccessHandler] Access denied popup closed.");
                evt.ResultSource.TrySetResult(WorldAccessResult.Denied);
            }
            catch (OperationCanceledException)
            {
                evt.ResultSource.TrySetResult(WorldAccessResult.Denied);
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.REALM);
                evt.ResultSource.TrySetResult(WorldAccessResult.Denied);
            }
        }

        private async UniTask HandlePasswordRequiredAsync(CheckWorldAccessEvent evt, string? ownerAddress)
        {
            string? owner = ownerAddress ?? evt.OwnerAddress;
            string? errorMessage = null;

            for (int attempt = 0; attempt < MAX_PASSWORD_ATTEMPTS; attempt++)
            {
                try
                {
                    var popupParams = new PrivateWorldPopupParams(evt.WorldName, PrivateWorldPopupMode.PasswordRequired, owner)
                    {
                        ErrorMessage = errorMessage
                    };

                    await mvcManager.ShowAsync(PrivateWorldPopupController.IssueCommand(popupParams));

                    if (popupParams.Result == PrivateWorldPopupResult.Cancelled)
                    {
                        ReportHub.Log(ReportCategory.REALM, "[PrivateWorldAccessHandler] Password popup cancelled.");
                        evt.ResultSource.TrySetResult(WorldAccessResult.PasswordCancelled);
                        return;
                    }

                    if (popupParams.Result == PrivateWorldPopupResult.PasswordSubmitted)
                    {
                        bool valid = await worldPermissionsService.ValidatePasswordAsync(evt.WorldName, popupParams.EnteredPassword ?? string.Empty, CancellationToken.None);
                        if (valid)
                        {
                            ReportHub.Log(ReportCategory.REALM, "[PrivateWorldAccessHandler] Password accepted. Access allowed.");
                            evt.ResultSource.TrySetResult(WorldAccessResult.Allowed);
                            return;
                        }
                        ReportHub.Log(ReportCategory.REALM, $"[PrivateWorldAccessHandler] Invalid password (attempt {attempt + 1}/{MAX_PASSWORD_ATTEMPTS}).");
                        errorMessage = "Incorrect password. Please try again.";
                    }
                }
                catch (OperationCanceledException)
                {
                    evt.ResultSource.TrySetResult(WorldAccessResult.PasswordCancelled);
                    return;
                }
                catch (Exception ex)
                {
                    ReportHub.LogException(ex, ReportCategory.REALM);
                    evt.ResultSource.TrySetResult(WorldAccessResult.CheckFailed);
                    return;
                }
            }

            evt.ResultSource.TrySetResult(WorldAccessResult.PasswordCancelled);
        }
    }
}
