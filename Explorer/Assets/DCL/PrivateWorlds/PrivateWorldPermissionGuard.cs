using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// When the player is in a world, periodically checks permissions via <see cref="IWorldPermissionsService"/>.
    /// If access is no longer allowed, teleports the player to Genesis Plaza.
    /// </summary>
    public class PrivateWorldPermissionGuard
    {
        private static readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromSeconds(5);

        private readonly IRealmData realmData;
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IRealmNavigator realmNavigator;
        private readonly CancellationTokenSource disposeCts = new();

        private CancellationTokenSource? loopCts;

        /// <param name="realmData">Current realm data; used to detect world entry/exit and to read the world name for permission checks.</param>
        /// <param name="worldPermissionsService">Service used to check if the current user still has access to the world.</param>
        /// <param name="realmNavigator">Used to teleport the player to Genesis Plaza when access is denied.</param>
        public PrivateWorldPermissionGuard(
            IRealmData realmData,
            IWorldPermissionsService worldPermissionsService,
            IRealmNavigator realmNavigator)
        {
            this.realmData = realmData;
            this.worldPermissionsService = worldPermissionsService;
            this.realmNavigator = realmNavigator;

            realmData.RealmType.OnUpdate += OnRealmTypeChanged;
            OnRealmTypeChanged(realmData.RealmType.Value);
        }

        public void Dispose()
        {
            realmData.RealmType.OnUpdate -= OnRealmTypeChanged;
            StopCheckLoop();
            disposeCts.SafeCancelAndDispose();
        }

        private void OnRealmTypeChanged(RealmKind realmKind)
        {
            if (realmKind is RealmKind.World)
                StartCheckLoop();
            else
                StopCheckLoop();
        }

        private void StartCheckLoop()
        {
            StopCheckLoop();
            loopCts = new CancellationTokenSource();
            RunCheckLoopAsync(loopCts.Token).Forget();
        }

        private void StopCheckLoop()
        {
            loopCts.SafeCancelAndDispose();
            loopCts = null;
        }

        private async UniTaskVoid RunCheckLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && realmData.RealmType.Value is RealmKind.World)
                {
                    await UniTask.Delay(CHECK_INTERVAL, cancellationToken: ct);
                    if (ct.IsCancellationRequested)
                        break;

                    string worldName = realmData.RealmName;
                    if (string.IsNullOrEmpty(worldName))
                        continue;

                    WorldAccessCheckContext context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);
                    if (ct.IsCancellationRequested)
                        break;

                    if (context.Result is WorldAccessCheckResult.AccessDenied)
                    {
                        ReportHub.Log(ReportCategory.REALM,
                            $"[PrivateWorlds] Access denied for '{worldName}' ({context.Result}), teleporting to Genesis");
                        realmNavigator.TeleportToParcelAsync(Vector2Int.zero, disposeCts.Token, false).Forget();
                        return;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.REALM);
            }
        }
    }
}
