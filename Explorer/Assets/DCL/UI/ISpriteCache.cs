using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    /// <summary>
    /// Provides a way to download images from a server and store them to be reused later.
    /// </summary>
    public interface ISpriteCache
    {
        /// <summary>
        /// A fast non-async way to get a cached image, assuming it exists.
        /// </summary>
        /// <param name="imageUrl">The URL from which the image was originally got.</param>
        /// <returns>The sprite that envelopes the image, or null if the URL is not in the cache.</returns>
        Sprite? GetCachedSprite(string imageUrl);

        /// <summary>
        /// Obtains an image from the cache and, if it is not there, it downloads it from the server and stores the result.
        /// </summary>
        /// <param name="imageUrl">The URL where to get the image from.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The sprite that envelopes the image, or null if there was a problem obtaining the image.</returns>
        UniTask<Sprite?> GetSpriteAsync(string imageUrl, CancellationToken ct = default);

        /// <summary>
        /// Obtains an image from the cache and, if it is not there, it downloads it from the server and stores the result.
        /// </summary>
        /// <param name="imageUrl">The URL where to get the image from.</param>
        /// <param name="useKtx">Indicates whether KTX is used when requesting the image from the server.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The sprite that envelopes the image, or null if there was a problem obtaining the image.</returns>
        UniTask<Sprite?> GetSpriteAsync(string imageUrl, bool useKtx, CancellationToken ct = default);

        /// <summary>
        /// Allows to include an external image to the cache or replace an existing one.
        /// </summary>
        /// <param name="imageUrl">The URL from where the image was obtained.</param>
        /// <param name="imageContent">The new content. If the image already exists, it will be replaced.</param>
        void AddOrReplaceCachedSprite(string? imageUrl, Sprite imageContent);

        /// <summary>
        /// Empties all the buffers and cache.
        /// </summary>
        void Clear();
    }
}
