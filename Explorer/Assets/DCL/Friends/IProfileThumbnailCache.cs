using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System.Threading;
using UnityEngine;

namespace DCL.Friends
{
    public interface IProfileThumbnailCache
    {
        Sprite? GetThumbnail(string userId);
        UniTask<Sprite?> GetThumbnail(Profile profile, CancellationToken ct = default);
        void SetThumbnail(string userId, Sprite sprite);
    }
}
