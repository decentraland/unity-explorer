using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles
{
    public interface IThumbnailCache
    {
        Sprite? GetThumbnail(string id);
        UniTask<Sprite?> GetThumbnailAsync(string id, string thumbnailUrl, CancellationToken ct = default);
    }
}
