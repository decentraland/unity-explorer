using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Input;
using DCL.LiveKit.Public;
using DCL.Utilities;
using DCL.VoiceChat;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Single source of mouth-animation input. Listens to chat (text) and voice-chat (LiveKit
    ///     active speakers + call status) and writes <see cref="AvatarMouthInputComponent"/> on the
    ///     matching avatar entity. Per ADR-317 lip-sync is local-only — events are interpreted by
    ///     each client and never propagated.
    ///     <para>
    ///         Per-participant coalescing: many events for the same wallet within one frame collapse
    ///         to a single dict entry; remote application happens through a single query that drains
    ///         the dict by <see cref="AvatarShapeComponent.ID"/>. Chat takes priority over voice —
    ///         handled downstream by <c>AvatarFacialExpressionSystem.StepMouthAnimation</c> which
    ///         consumes <see cref="AvatarMouthInputComponent.MessageIsDirty"/> first; the voice loop
    ///         only resumes when no chat text is animating.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class MouthAnimationSystem : BaseUnityLoopSystem
    {
        public struct PendingInput
        {
            public string? Message;     // null when no chat update happened this frame
            public bool? IsSpeaking;    // null when no speaking change happened this frame
        }

        private readonly IChatHistory chatHistory;
        private readonly IRoom voiceChatRoom;
        private readonly IDisposable statusSubscription;

        private PendingInput pendingSelf;
        private readonly Dictionary<string, PendingInput> pendingByWallet = new ();

        private readonly HashSet<string> activeSpeakers = new ();
        private readonly HashSet<string> nextActiveSpeakers = new ();

        internal MouthAnimationSystem(
            World world,
            IChatHistory chatHistory,
            IRoom voiceChatRoom,
            IVoiceChatOrchestratorState voiceChatOrchestratorState) : base(world)
        {
            this.chatHistory = chatHistory;
            this.voiceChatRoom = voiceChatRoom;

            chatHistory.MessageAdded += OnChatMessageAdded;
            voiceChatRoom.ActiveSpeakers.Updated += OnActiveSpeakersUpdated;
            voiceChatRoom.Participants.UpdatesFromParticipant += OnParticipantUpdated;
            statusSubscription = voiceChatOrchestratorState.CurrentCallStatus.Subscribe(OnCallStatusChanged);
        }

        protected override void OnDispose()
        {
            chatHistory.MessageAdded -= OnChatMessageAdded;
            voiceChatRoom.ActiveSpeakers.Updated -= OnActiveSpeakersUpdated;
            voiceChatRoom.Participants.UpdatesFromParticipant -= OnParticipantUpdated;
            statusSubscription?.Dispose();
        }

        protected override void Update(float t)
        {
            if (pendingSelf.Message != null || pendingSelf.IsSpeaking.HasValue)
            {
                ApplyToSelfQuery(World, pendingSelf);
                pendingSelf = default;
            }

            if (pendingByWallet.Count > 0)
                ApplyToRemoteQuery(World);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void ApplyToSelf([Data] PendingInput pending, ref AvatarMouthInputComponent input)
        {
            ApplyPending(pending, ref input);
        }

        /// <summary>
        ///     Iterates remote avatar entities and consumes their pending input from
        ///     <see cref="pendingByWallet"/> by <see cref="AvatarShapeComponent.ID"/>. Entries with
        ///     no matching avatar entity stay in the dict and are retried next frame (covers the
        ///     race where chat/voice events arrive before the avatar is instantiated).
        /// </summary>
        [Query]
        [All(typeof(AvatarMouthInputComponent), typeof(AvatarShapeComponent))]
        [None(typeof(PlayerComponent), typeof(DeleteEntityIntention))]
        private void ApplyToRemote(in AvatarShapeComponent avatarShape, ref AvatarMouthInputComponent input)
        {
            if (string.IsNullOrEmpty(avatarShape.ID)) return;

            if (!pendingByWallet.Remove(avatarShape.ID, out PendingInput pending))
                return;

            ApplyPending(pending, ref input);
        }

        private static void ApplyPending(PendingInput pending, ref AvatarMouthInputComponent input)
        {
            if (pending.Message != null)
            {
                input.PendingMessage = pending.Message;
                input.MessageIsDirty = true;
            }

            if (pending.IsSpeaking.HasValue)
                input.IsVoiceChatSpeaking = pending.IsSpeaking.Value;
        }

        private void OnChatMessageAdded(ChatChannel _, ChatMessage msg, int __)
        {
            if (msg.IsSystemMessage) return;

            if (msg.IsSentByOwnUser)
                pendingSelf.Message = msg.Message;
            else
                StashRemoteMessage(msg.SenderWalletAddress, msg.Message);
        }

        private void StashRemoteMessage(string walletId, string message)
        {
            if (pendingByWallet.TryGetValue(walletId, out PendingInput existing))
            {
                existing.Message = message;
                pendingByWallet[walletId] = existing;
            }
            else
                pendingByWallet[walletId] = new PendingInput { Message = message };
        }

        private void StashSpeaking(string walletId, bool isSpeaking, bool isSelf)
        {
            if (isSelf)
            {
                pendingSelf.IsSpeaking = isSpeaking;
                return;
            }

            if (pendingByWallet.TryGetValue(walletId, out PendingInput existing))
            {
                existing.IsSpeaking = isSpeaking;
                pendingByWallet[walletId] = existing;
            }
            else
                pendingByWallet[walletId] = new PendingInput { IsSpeaking = isSpeaking };
        }

        private void OnActiveSpeakersUpdated()
        {
            nextActiveSpeakers.Clear();

            foreach (string speakerId in voiceChatRoom.ActiveSpeakers)
            {
                nextActiveSpeakers.Add(speakerId);

                if (!activeSpeakers.Contains(speakerId))
                    EnqueueSpeaking(speakerId, true);
            }

            foreach (string oldSpeakerId in activeSpeakers)
                if (!nextActiveSpeakers.Contains(oldSpeakerId))
                    EnqueueSpeaking(oldSpeakerId, false);

            activeSpeakers.Clear();
            activeSpeakers.UnionWith(nextActiveSpeakers);
        }

        private void EnqueueSpeaking(string participantId, bool isSpeaking)
        {
            bool isSelf = voiceChatRoom.Participants.LocalParticipant()?.Identity == participantId;
            StashSpeaking(participantId, isSpeaking, isSelf);
        }

        private void OnParticipantUpdated(LKParticipant participant, UpdateFromParticipant update)
        {
            if (update != UpdateFromParticipant.Disconnected) return;

            StashSpeaking(participant.Identity, false, isSelf: false);
            activeSpeakers.Remove(participant.Identity);
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    pendingSelf.IsSpeaking = false;
                    OnActiveSpeakersUpdated();
                    break;

                case VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    pendingSelf.IsSpeaking = false;
                    activeSpeakers.Clear();

                    foreach ((string participantId, _) in voiceChatRoom.Participants.RemoteParticipantIdentities())
                        StashSpeaking(participantId, false, isSelf: false);

                    break;
            }
        }
    }
}
