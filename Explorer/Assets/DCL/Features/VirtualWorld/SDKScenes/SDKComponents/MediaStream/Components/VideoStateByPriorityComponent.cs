﻿using Arch.Core;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    /// Data related to the calculation of the priority of a video and its corresponding state (playing or paused).
    /// </summary>
    public struct VideoStateByPriorityComponent
    {
        /// <summary>
        /// The final score used to determine the priority of the video.
        /// </summary>
        public float Score;

        /// <summary>
        /// Whether the video should be playing or not, if prioritization mechanism did not exist.
        /// </summary>
        public bool WantsToPlay;

        /// <summary>
        /// The entity that has the MediaPlayer.
        /// </summary>
        public readonly Entity Entity;

        /// <summary>
        /// Whether the video should be playing according to its priority.
        /// </summary>
        public bool IsPlaying;

        /// <summary>
        /// The time when the video was played manually.
        /// </summary>
        public float MediaPlayStartTime;

        /// <summary>
        /// A mesh renderer used for visually debugging the priority of each video.
        /// </summary>
        public MeshRenderer? DebugPrioritySign;

        public VideoStateByPriorityComponent(Entity entity, bool wantsToPlay)
        {
            Entity = entity;
            WantsToPlay = wantsToPlay;

            Score = 0.0f;
            IsPlaying = false;
            MediaPlayStartTime = float.MinValue;
            DebugPrioritySign = null;
        }
    }
}
