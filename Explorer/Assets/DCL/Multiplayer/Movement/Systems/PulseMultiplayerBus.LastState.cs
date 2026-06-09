using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using Decentraland.Pulse;
using System.Diagnostics;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        // Captured on the main thread by Send / Send / SendStop. Read on the Pulse connection
        // thread by BuildInitialState when assembling a reconnect handshake.
        private readonly object initialStateSync = new ();
        private NetworkMovementMessage? lastLocalMovement;
        private string? lastLocalEmoteId;
        private uint lastLocalEmoteDurationMs;
        private long lastLocalEmoteStartTicks;
        private AvatarEmoteMask lastLocalEmoteMask;

        private void StoreLastMovement(in NetworkMovementMessage message)
        {
            lock (initialStateSync)
            {
                lastLocalMovement = message;

                if (!message.isEmoting)
                {
                    lastLocalEmoteId = null;
                    lastLocalEmoteDurationMs = 0;
                    lastLocalEmoteStartTicks = 0;
                }
            }
        }

        private void StoreLastEmoteStart(URN emoteId, uint durationMs, NetworkMovementMessage? playerState, AvatarEmoteMask mask)
        {
            lock (initialStateSync)
            {
                lastLocalEmoteId = emoteId;
                lastLocalEmoteDurationMs = durationMs;
                lastLocalEmoteStartTicks = Stopwatch.GetTimestamp();
                lastLocalEmoteMask = mask;

                if (playerState.HasValue)
                    lastLocalMovement = playerState.Value;
            }
        }

        private void ClearLastEmote()
        {
            lock (initialStateSync)
            {
                lastLocalEmoteId = null;
                lastLocalEmoteDurationMs = 0;
                lastLocalEmoteStartTicks = 0;
            }
        }

        private void WriteInitialState(HandshakeRequest handshakeRequest)
        {
            NetworkMovementMessage message;
            string? capturedEmoteId;
            uint capturedDurationMs;
            long capturedStartTicks;
            AvatarEmoteMask? capturedEmoteMask;

            lock (initialStateSync)
            {
                if (!lastLocalMovement.HasValue)
                {
                    handshakeRequest.InitialState = null;
                    return;
                }

                message = lastLocalMovement.Value;
                capturedEmoteId = lastLocalEmoteId;
                capturedDurationMs = lastLocalEmoteDurationMs;
                capturedStartTicks = lastLocalEmoteStartTicks;
                capturedEmoteMask = lastLocalEmoteMask;
            }

            // Subsequent reconnection messages will reuse the allocated state
            PlayerInitialState initialState = handshakeRequest.InitialState ??= new PlayerInitialState();
            initialState.Realm = realmData.RealmName;
            PlayerState state = handshakeRequest.InitialState.State ??= new PlayerState();
            WritePlayerState(message, state, parcelEncoder);

            if (!string.IsNullOrEmpty(capturedEmoteId))
            {
                initialState.EmoteId = capturedEmoteId;

                if (capturedEmoteMask != AvatarEmoteMask.AemFullBody)
                    initialState.EmoteMask = (int)capturedEmoteMask;

                if (capturedDurationMs > 0)
                    initialState.EmoteDurationMs = capturedDurationMs;

                long elapsedTicks = Stopwatch.GetTimestamp() - capturedStartTicks;

                if (elapsedTicks > 0)
                    initialState.EmoteStartOffsetMs = (uint)(elapsedTicks * 1000 / Stopwatch.Frequency);
            }
        }
    }
}
