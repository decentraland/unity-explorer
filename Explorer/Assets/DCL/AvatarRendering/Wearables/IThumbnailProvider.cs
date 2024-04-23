using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables
{
    public interface IThumbnailProvider
    {
        UniTask<Sprite?> GetAsync(IAvatarAttachment avatarAttachment, CancellationToken ct);
    }
}
