using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
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
        private readonly IWorldCommsSecret worldCommsSecret;
        private readonly IChatHistory chatHistory;
        private readonly CancellationTokenSource disposeCts = new();

        private CancellationTokenSource? loopCts;

        /// <param name="realmData">Current realm data; used to detect world entry/exit and to read the world name for permission checks.</param>
        /// <param name="worldPermissionsService">Service used to check if the current user still has access to the world.</param>
        /// <param name="realmNavigator">Used to teleport the player to Genesis Plaza when access is denied.</param>
        /// <param name="worldCommsSecret">Contains validated world password. Used to distinguish "password required" from "already authenticated".</param>
        /// <param name="chatHistory">Used to post a system message when access is revoked while already inside a world.</param>
        public PrivateWorldPermissionGuard(
            IRealmData realmData,
            IWorldPermissionsService worldPermissionsService,
            IRealmNavigator realmNavigator,
            IWorldCommsSecret worldCommsSecret,
            IChatHistory chatHistory)
        {
            this.realmData = realmData;
            this.worldPermissionsService = worldPermissionsService;
            this.realmNavigator = realmNavigator;
            this.worldCommsSecret = worldCommsSecret;
            this.chatHistory = chatHistory;

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

                    // NOTE: Add timeout here to be sure
                    WorldAccessCheckContext context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);
                    if (ct.IsCancellationRequested)
                        break;

                    // Clear stale secret while in non-password worlds. This prevents a secret from a previous world
                    // from making PasswordRequired checks look "authenticated" after permissions change.
                    if (context.Result is WorldAccessCheckResult.Allowed &&
                        context.AccessInfo?.AccessType is not WorldAccessType.SharedSecret &&
                        !string.IsNullOrEmpty(worldCommsSecret.Secret))
                    {
                        worldCommsSecret.Secret = null;
                    }

                    bool accessRevoked = context.Result is WorldAccessCheckResult.AccessDenied
                                         || (context.Result is WorldAccessCheckResult.PasswordRequired && string.IsNullOrEmpty(worldCommsSecret.Secret));

                    if (accessRevoked)
                    {
                        ReportHub.Log(ReportCategory.REALM,
                            $"[PrivateWorlds] Access revoked for '{worldName}' ({context.Result}), teleporting to Genesis");
                        chatHistory.AddMessage(
                            ChatChannel.NEARBY_CHANNEL_ID,
                            ChatChannel.ChatChannelType.NEARBY,
                            ChatMessage.NewFromSystem("Permissions for this world changed. You were returned to Genesis Plaza."));
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
