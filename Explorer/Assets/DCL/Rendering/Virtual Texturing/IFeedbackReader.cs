using System;
using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Interface for components that read back feedback buffers from GPU to CPU memory.
    /// 
    /// The feedback buffer contains information about which virtual texture pages
    /// are needed for the current view, allowing the system to prioritize texture
    /// loading based on visibility.
    /// </summary>
    public interface IFeedbackReader
    {
        /// <summary>
        /// Event fired when feedback texture readback is complete.
        /// Subscribers can process the feedback data to determine which texture
        /// pages need to be loaded or activated.
        /// </summary>
        event Action<Texture2D> OnFeedbackReadComplete;
    }
}