using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables
{
    public interface IThumbnailProvider
    {
        /// <summary>
        ///     Bounds the wait on a thumbnail load to a sensible UX budget, so requests that stall
        ///     in the streaming pipeline (e.g. budget starvation, lost promises, curl aborts)
        ///     do not leave callers spinning indefinitely.
        /// </summary>
        public const int DEFAULT_TIMEOUT_MS = 30_000;

        UniTask<Sprite> GetAsync(IThumbnailAttachment avatarAttachment, CancellationToken ct, int timeoutMs = DEFAULT_TIMEOUT_MS);
    }
}
