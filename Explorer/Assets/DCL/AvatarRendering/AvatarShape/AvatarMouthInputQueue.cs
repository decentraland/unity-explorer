using Arch.Core;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Buffer for mouth-animation inputs originating outside the ECS update loop
    ///     (voice-chat speakers, chat messages). External services enqueue here; the avatar
    ///     facial expression system drains and applies the entries each frame.
    /// </summary>
    public sealed class AvatarMouthInputQueue
    {
        private readonly List<(Entity entity, bool isSpeaking)> pendingSpeaking = new();
        private readonly List<(Entity entity, string message)> pendingMessages = new();

        public void EnqueueSpeaking(Entity entity, bool isSpeaking) =>
            pendingSpeaking.Add((entity, isSpeaking));

        public void EnqueueMessage(Entity entity, string message) =>
            pendingMessages.Add((entity, message));

        public void DrainTo(
            List<(Entity entity, bool isSpeaking)> speakingOut,
            List<(Entity entity, string message)> messagesOut)
        {
            speakingOut.AddRange(pendingSpeaking);
            pendingSpeaking.Clear();

            messagesOut.AddRange(pendingMessages);
            pendingMessages.Clear();
        }
    }
}