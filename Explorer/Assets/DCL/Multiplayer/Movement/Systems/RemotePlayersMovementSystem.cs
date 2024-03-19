using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using UnityEngine;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemotePlayersMovementSystem : BaseUnityLoopSystem
    {
        // Amount of positions with delta below MinPositionDelta, that will be skip in one frame.
        private const int SAME_POSITION_BATCH = 10;

        // Amount of positions with timestamp that older than timestamp of the last passed message, that will be skip in one frame.
        private const int OLD_MESSAGES_BATCH = 10;
        private const int BEHIND_EXTRAPOLATION_BATCH = 10;
        private const float ZERO_VELOCITY_THRESHOLD = 0.01f;

        private readonly IMultiplayerMovementSettings settings;
        private readonly MultiplayerMovementMessageBus messageBus;

        public RemotePlayersMovementSystem(World world, MultiplayerMovementMessageBus messageBus, IMultiplayerMovementSettings settings) : base(world)
        {
            this.settings = settings;
            this.messageBus = messageBus;
        }

        protected override void Update(float t)
        {
            UpdateRemotePlayersMovementQuery(World, t);
        }

        private static void HandleFirstMessage(ref CharacterTransform transComp, NetworkMovementMessage firstRemote, ref RemotePlayerMovementComponent remotePlayerMovement)
        {
            transComp.Transform.position = firstRemote.position;

            remotePlayerMovement.AddPassed(firstRemote, wasTeleported: true);
            remotePlayerMovement.Initialized = true;
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateRemotePlayersMovement([Data] float deltaTime, ref CharacterTransform transComp,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp, ref ExtrapolationComponent extComp)
        {
            if (!messageBus.InboxByParticipantMap.TryGetValue(remotePlayerMovement.PlayerWalletId, out SimplePriorityQueue<NetworkMovementMessage>? playerInbox))
                return;

            settings.InboxCount = playerInbox.Count;

            // First message
            if (!remotePlayerMovement.Initialized && playerInbox.Count > 0)
            {
                HandleFirstMessage(ref transComp, playerInbox.Dequeue(), ref remotePlayerMovement);
                if (playerInbox.Count == 0) return;
            }

            if (intComp.Enabled)
            {
                deltaTime = Interpolate(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp);
                if (deltaTime <= 0) return;
            }

            // Filter old messages that arrived too late
            for (var i = 0; i < OLD_MESSAGES_BATCH && playerInbox.Count > 0 && playerInbox.First.timestamp <= remotePlayerMovement.PastMessage.timestamp; i++)
                playerInbox.Dequeue();

            // When there is no messages, we extrapolate
            if (playerInbox.Count == 0 && settings.UseExtrapolation && remotePlayerMovement is { Initialized: true, WasTeleported: false })
            {
                if (!extComp.Enabled && remotePlayerMovement.PastMessage.velocity.sqrMagnitude > settings.ExtrapolationSettings.MinSpeed)
                    extComp.Restart(from: remotePlayerMovement.PastMessage, settings.ExtrapolationSettings.TotalMoveDuration);

                if (extComp.Enabled)
                {
                    Extrapolation.Execute(deltaTime, ref transComp, ref extComp, settings.ExtrapolationSettings);
                    return;
                }
            }

            if (playerInbox.Count > 0)
                HandleNewMessage(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp, ref extComp, playerInbox);
        }

        private void HandleNewMessage(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp, ref ExtrapolationComponent extComp, SimplePriorityQueue<NetworkMovementMessage> playerInbox)
        {
            NetworkMovementMessage remote = playerInbox.Dequeue();
            var isBlend = false;

            if (extComp.Enabled)
            {
                // if we success with stop of extrapolation, then we can start to blend
                if (StopExtrapolationIfCan(ref remote, ref transComp, ref remotePlayerMovement, ref extComp, playerInbox))
                    isBlend = true;
                else return;
            }

            if (CanTeleport(remotePlayerMovement, remote))
            {
                isBlend = false;
                TeleportFiltered(ref remote, ref transComp, ref remotePlayerMovement, playerInbox);

                if (playerInbox.Count == 0) return;

                remote = playerInbox.Dequeue();
            }

            StartInterpolation(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp, remote, isBlend);
        }

        private static bool StopExtrapolationIfCan(ref NetworkMovementMessage remote, ref CharacterTransform transComp,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref ExtrapolationComponent extComp, SimplePriorityQueue<NetworkMovementMessage> playerInbox)
        {
            float minExtTimestamp = extComp.Start.timestamp + Mathf.Min(extComp.Time, extComp.TotalMoveDuration);

            // Filter all messages that are behind in time (otherwise we will run back)
            for (var i = 0; i < BEHIND_EXTRAPOLATION_BATCH && playerInbox.Count > 0 && remote.timestamp <= minExtTimestamp; i++)
                remote = playerInbox.Dequeue();

            if (remote.timestamp <= minExtTimestamp)
                return false;

            // Stop extrapolating and proceed to blending
            {
                extComp.Stop();

                var local = new NetworkMovementMessage
                {
                    timestamp = minExtTimestamp, // we need to take the timestamp that < remote.timestamp

                    position = transComp.Transform.position,
                    velocity = extComp.Start.velocity,

                    animState = extComp.Start.animState,
                    isStunned = extComp.Start.isStunned,
                };

                remotePlayerMovement.AddPassed(local);
            }

            return true;
        }

        private void TeleportFiltered(ref NetworkMovementMessage remote, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            SimplePriorityQueue<NetworkMovementMessage> playerInbox)
        {
            // Filter messages with the same position
            if (settings.InterpolationSettings.UseSpeedUp)
                for (var i = 0; i < SAME_POSITION_BATCH && playerInbox.Count > 0 && Vector3.SqrMagnitude(playerInbox.First.position - remote.position) < settings.MinPositionDelta; i++)
                    remote = playerInbox.Dequeue();

            transComp.Transform.position = remote.position;
            remotePlayerMovement.AddPassed(remote, wasTeleported: true);
        }

        private bool CanTeleport(in RemotePlayerMovementComponent remotePlayerMovement, in NetworkMovementMessage remote)
        {
            float sqrDistance = Vector3.SqrMagnitude(remotePlayerMovement.PastMessage.position - remote.position);

            return sqrDistance > settings.MinTeleportDistance ||
                   (settings.InterpolationSettings.UseSpeedUp && sqrDistance < settings.MinPositionDelta);
        }

        private void StartInterpolation(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp, in NetworkMovementMessage remote, bool isBlend)
        {
            RemotePlayerInterpolationSettings? intSettings = settings.InterpolationSettings;

            InterpolationType spline = intSettings.UseBlend ? intSettings.BlendType :
                remote.velocity.sqrMagnitude < ZERO_VELOCITY_THRESHOLD || remotePlayerMovement.PastMessage.velocity.sqrMagnitude < ZERO_VELOCITY_THRESHOLD ? InterpolationType.Linear :
                intSettings.InterpolationType;

            intComp.Restart(remotePlayerMovement.PastMessage, remote, spline);

            if (intSettings.UseBlend && isBlend)
                SlowDownBlend(ref intComp, intSettings.MaxBlendSpeed);
            else if (intSettings.UseSpeedUp)
                SpeedUpForCatchingUp(ref intComp, settings.InboxCount);

            transComp.Transform.position = intComp.Start.position;

            // TODO (Vit): Restart in loop until (unusedTime <= 0) ?
            float unusedTime = Interpolate(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp);
        }

        private float Interpolate(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp)
        {
            float unusedTime = Interpolation.Execute(deltaTime, ref transComp, ref intComp, settings.InterpolationSettings.LookAtTimeDelta);

            if (intComp.Time < intComp.TotalDuration)
                return -1;

            intComp.Stop();
            remotePlayerMovement.AddPassed(intComp.End);

            return unusedTime;
        }

        private static void SlowDownBlend(ref InterpolationComponent intComp, float maxBlendSpeed)
        {
            float positionDiff = Vector3.Distance(intComp.Start.position, intComp.End.position);
            float speed = positionDiff / intComp.TotalDuration;

            // we can do more precise, but more expensive solution -  if (speed * speed > Mathf.Max(maxBlendSpeed * maxBlendSpeed, intComp.Start.velocity.sqrMagnitude, intComp.End.velocity.sqrMagnitude))
            if (speed > maxBlendSpeed)
                intComp.TotalDuration = positionDiff / maxBlendSpeed;
        }

        private void SpeedUpForCatchingUp(ref InterpolationComponent intComp, int inboxMessages)
        {
            if (inboxMessages > settings.InterpolationSettings.CatchUpMessagesMin)
            {
                float correctionTime = inboxMessages * UnityEngine.Time.smoothDeltaTime;
                intComp.TotalDuration = Mathf.Max(intComp.TotalDuration - correctionTime, intComp.TotalDuration / settings.InterpolationSettings.MaxSpeedUpTimeDivider);
            }
        }
    }
}
