using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using System;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.Play
{
    /// <summary>
    /// A component that provides notifications for the state changes of the animator state machine of a character.
    /// </summary>
    public class AvatarStateMachineEventHandler
    {
        private readonly Entity entity;

        /// <summary>
        /// Raised when exiting both Emote and Emote Loop states.
        /// </summary>
        public Action<Entity, AvatarStateMachineEventHandler>? EmoteStateExiting;

        public AvatarStateMachineEventHandler(Entity entity, Animator animator)
        {
            this.entity = entity;
            animator.GetBehaviour<AvatarStateMachineBehaviour>().EmoteStateExiting += OnEmoteStateExiting;
        }

        private void OnEmoteStateExiting()
        {
            EmoteStateExiting?.Invoke(entity, this);
        }
    }
}
