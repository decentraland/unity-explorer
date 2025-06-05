using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles
{
    public interface IProfileThumbnailCache
    {
        Sprite? GetThumbnail(string userId);

        UniTask<Sprite?> GetThumbnailAsync(string userId, Uri thumbnailUrl, CancellationToken ct = default);
    }
}
