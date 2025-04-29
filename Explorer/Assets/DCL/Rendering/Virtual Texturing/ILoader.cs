using System;
using UnityEngine;

namespace VirtualTexture
{
    /// <summary>
    /// Interface for components that load virtual texture pages.
    /// 
    /// Loaders are responsible for retrieving texture data for specific page table entries,
    /// potentially from various sources like disk, memory, or network, and making the data
    /// available for the virtual texturing system.
    /// </summary>
    public interface ILoader
    {
        /// <summary>
        /// Event fired when a load request completes successfully.
        /// 
        /// The array of Texture2D objects contains the loaded texture data for each layer
        /// of the virtual texture (e.g., diffuse, normal, specular maps).
        /// </summary>
        event Action<LoadRequest, Texture2D[]> OnLoadComplete;

        /// <summary>
        /// Creates a new request to load a specific virtual texture page.
        /// 
        /// </summary>
        /// <param name="x">X-coordinate in the page table</param>
        /// <param name="y">Y-coordinate in the page table</param>
        /// <param name="mip">Mipmap level to load</param>
        /// <returns>
        /// A LoadRequest object representing the request, or null if the request
        /// was rejected (e.g., if an identical request is already in progress)
        /// </returns>
        LoadRequest Request(int x, int y, int mip);
    }
}