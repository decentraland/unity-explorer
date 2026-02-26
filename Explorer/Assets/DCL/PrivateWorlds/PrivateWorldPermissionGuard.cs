using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using ECS;
using ECS.SceneLifeCycle.Realm;
using LiveKit.Proto;
using LiveKit.Rooms;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using ChatMessage = DCL.Chat.History.ChatMessage;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// When the player is in a world, checks permissions via <see cref="IWorldPermissionsService"/> on
    /// relevant comms disconnect signals and teleports the player to Genesis Plaza if access is no longer allowed.
    /// </summary>
    public class PrivateWorldPermissionGuard
    {
        private readonly IRoomHub roomHub;
        private readonly IRealmData realmData;
        private readonly IWorldPermissionsService worldPermissionsService;
        private readonly IRealmNavigator realmNavigator;
        private readonly IWorldCommsSecret worldCommsSecret;
        private readonly IChatHistory chatHistory;
        private readonly CancellationTokenSource disposeCts = new();

        private bool isPermissionCheckRunning;

        public PrivateWorldPermissionGuard(
            IRoomHub roomHub,
            IRealmData realmData,
            IWorldPermissionsService worldPermissionsService,
            IRealmNavigator realmNavigator,
            IWorldCommsSecret worldCommsSecret,
            IChatHistory chatHistory)
        {
            this.roomHub = roomHub;
            this.realmData = realmData;
            this.worldPermissionsService = worldPermissionsService;
            this.realmNavigator = realmNavigator;
            this.worldCommsSecret = worldCommsSecret;
            this.chatHistory = chatHistory;

            roomHub.IslandRoom().ConnectionUpdated += OnIslandRoomConnectionUpdated;
        }

        public void Dispose()
        {
            roomHub.IslandRoom().ConnectionUpdated -= OnIslandRoomConnectionUpdated;
            disposeCts.SafeCancelAndDispose();
        }

        private void OnIslandRoomConnectionUpdated(IRoom _, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason = null)
        {
            if (connectionUpdate != ConnectionUpdate.Disconnected)
                return;

            if (realmData.RealmType.Value is RealmKind.World && disconnectReason.HasValue)
            {
                ReportHub.Log(ReportCategory.REALM,
                    $"[PrivateWorlds] Island room disconnected with reason {disconnectReason.Value}");
            }

            if (disconnectReason != DisconnectReason.ParticipantRemoved)
                return;

            RequestPermissionCheck();
        }

        private void RequestPermissionCheck()
        {
            if (realmData.RealmType.Value is not RealmKind.World)
                return;

            if (isPermissionCheckRunning)
                return;

            isPermissionCheckRunning = true;
            RunPermissionCheckAsync(disposeCts.Token).Forget();
        }

        private async UniTaskVoid RunPermissionCheckAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.SwitchToMainThread(ct);
                await CheckCurrentWorldAccessAndTeleportIfRevokedAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.REALM);
            }
            finally
            {
                isPermissionCheckRunning = false;
            }
        }

        private async UniTask CheckCurrentWorldAccessAndTeleportIfRevokedAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested || realmData.RealmType.Value is not RealmKind.World)
                return;

            string worldName = realmData.RealmName;

            if (string.IsNullOrEmpty(worldName))
                return;

            WorldAccessCheckContext context = await worldPermissionsService.CheckWorldAccessAsync(worldName, ct);

            if (ct.IsCancellationRequested || realmData.RealmType.Value is not RealmKind.World)
                return;

            // Realm could have changed while the async permission check was in flight.
            if (!string.Equals(worldName, realmData.RealmName, StringComparison.OrdinalIgnoreCase))
                return;

            // NOTE:
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

            if (!accessRevoked)
                return;

            ReportHub.Log(ReportCategory.REALM,
                $"[PrivateWorlds] Access revoked for '{worldName}' ({context.Result}) after ParticipantRemoved disconnect, teleporting to Genesis");

            var teleportResult = await realmNavigator.TeleportToParcelAsync(Vector2Int.zero, disposeCts.Token, false);

            if (!teleportResult.Success)
            {
                ReportHub.LogWarning(ReportCategory.REALM,
                    $"[PrivateWorlds] Failed to teleport revoked user from '{worldName}' to Genesis after ParticipantRemoved disconnect. Waiting for the next permission check trigger.");
                return;
            }

            chatHistory.AddMessage(
                ChatChannel.NEARBY_CHANNEL_ID,
                ChatChannel.ChatChannelType.NEARBY,
                ChatMessage.NewFromSystem("Permissions for this world changed. You were returned to Genesis Plaza."));
        }
    }
}
