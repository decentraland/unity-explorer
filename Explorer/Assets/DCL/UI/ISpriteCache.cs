using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.UI
{
    public interface ISpriteCache
    {
        Sprite? GetCachedSprite(string imageUrl);
        UniTask<Sprite?> GetSpriteAsync(string imageUrl, CancellationToken ct = default);
        UniTask<Sprite?> GetSpriteAsync(string imageUrl, bool useKtx, CancellationToken ct = default);

        void Clear();
    }
}
