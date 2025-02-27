using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles
{
    public interface IProfileThumbnailCache
    {
        Sprite? GetThumbnail(string userId);
        UniTask<Sprite?> GetThumbnailAsync(Profile profile, CancellationToken ct = default);
        UniTask<Sprite?> GetThumbnailAsync(string userId, string thumbnailUrl, CancellationToken ct = default);
        void SetThumbnail(string userId, Sprite sprite);
    }
}
