using Arch.Core;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Thread-safe buffer for pending mouth-animation inputs that originate
    ///     outside the ECS update loop (voice-chat speakers, chat messages).
    ///     External services enqueue their changes here; <c>AvatarFacialExpressionSystem</c>
    ///     drains and applies them to ECS each frame on the main thread.
    ///     <para>
    ///         LiveKit SDK callbacks (<c>ActiveSpeakers.Updated</c>, <c>UpdatesFromParticipant</c>)
    ///         are NOT guaranteed to fire on the Unity main thread — see <c>VoiceChatParticipantsStateService</c>
    ///         for the same pattern using <c>PlayerLoopHelper.IsMainThread</c> guards.
    ///         All public methods therefore use <see cref="syncLock"/> for mutual exclusion.
    ///     </para>
    /// </summary>
    public sealed class AvatarMouthInputQueue
    {
        private readonly object syncLock = new();
        private readonly List<(Entity entity, bool isSpeaking)> pendingSpeaking = new();
        private readonly List<(Entity entity, string message)> pendingMessages = new();

        /// <summary>Enqueues a voice-chat speaking-state change for the given entity. Thread-safe.</summary>
        public void EnqueueSpeaking(Entity entity, bool isSpeaking)
        {
            lock (syncLock)
                pendingSpeaking.Add((entity, isSpeaking));
        }

        /// <summary>Enqueues a chat-message mouth animation request for the given entity. Thread-safe.</summary>
        public void EnqueueMessage(Entity entity, string message)
        {
            lock (syncLock)
                pendingMessages.Add((entity, message));
        }

        /// <summary>
        ///     Drains all pending entries into the supplied output lists and clears this queue.
        ///     Must only be called from the ECS main thread.
        /// </summary>
        public void DrainTo(
            List<(Entity entity, bool isSpeaking)> speakingOut,
            List<(Entity entity, string message)> messagesOut)
        {
            lock (syncLock)
            {
                speakingOut.AddRange(pendingSpeaking);
                pendingSpeaking.Clear();

                messagesOut.AddRange(pendingMessages);
                pendingMessages.Clear();
            }
        }
    }
}
