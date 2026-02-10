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
                if (evt.WorldName == CheckWorldAccessEvent.WORLD_NAME_TIMEOUT_TEST)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(20),DelayType.DeltaTime,PlayerLoopTiming.Update, evt.CancellationToken);
                    return;
                }

                var context = await worldPermissionsService.CheckWorldAccessAsync(evt.WorldName, evt.CancellationToken);

                switch (context.Result)
                {
                    case WorldAccessCheckResult.Allowed:
                        evt.ResultSource.TrySetResult(WorldAccessResult.Allowed);
                        return;

                    case WorldAccessCheckResult.CheckFailed:
                        evt.ResultSource.TrySetResult(WorldAccessResult.CheckFailed);
                        return;

                    case WorldAccessCheckResult.AccessDenied:
                        await ShowAccessDeniedAndSetResultAsync(evt, context.AccessInfo?.OwnerAddress);
                        return;

                    case WorldAccessCheckResult.PasswordRequired:
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
                        evt.ResultSource.TrySetResult(WorldAccessResult.PasswordCancelled);
                        return;
                    }

                    if (popupParams.Result == PrivateWorldPopupResult.PasswordSubmitted)
                    {
                        bool valid = await worldPermissionsService.ValidatePasswordAsync(evt.WorldName, popupParams.EnteredPassword ?? string.Empty, evt.CancellationToken);
                        if (valid)
                        {
                            evt.ResultSource.TrySetResult(WorldAccessResult.Allowed);
                            return;
                        }
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
