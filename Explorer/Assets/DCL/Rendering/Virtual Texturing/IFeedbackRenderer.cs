using System;
using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Interface for components that render feedback buffers for virtual texturing.
    /// 
    /// The feedback renderer creates a specialized render of the scene that captures
    /// information about which virtual texture pages are needed by the current view,
    /// including page indices and mipmap levels.
    /// </summary>
    public interface IFeedbackRenderer
    {
        /// <summary>
        /// Event fired when feedback rendering is complete.
        /// 
        /// The provided RenderTexture contains the feedback buffer data,
        /// where RGB channels typically encode page table coordinates and mipmap levels.
        /// </summary>
        event Action<RenderTexture> OnFeedbackRenderComplete;
    }
}