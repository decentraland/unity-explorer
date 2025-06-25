using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    /// <summary>
    ///
    /// </summary>
    public interface ISpriteCache
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <returns></returns>
        Sprite? GetCachedSprite(string imageUrl);

        /// <summary>
        ///
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        UniTask<Sprite?> GetSpriteAsync(string imageUrl, CancellationToken ct = default);

        /// <summary>
        ///
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <param name="useKtx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        UniTask<Sprite?> GetSpriteAsync(string imageUrl, bool useKtx, CancellationToken ct = default);

        /// <summary>
        ///
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <param name="imageContent"></param>
        void AddOrReplaceCachedSprite(string imageUrl, Sprite imageContent);

        /// <summary>
        ///
        /// </summary>
        void Clear();
    }
}
