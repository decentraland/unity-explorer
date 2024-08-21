using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using IAvatarAttachment = DCL.AvatarRendering.Loading.Components.IAvatarAttachment;

namespace DCL.AvatarRendering.Wearables
{
    public interface IThumbnailProvider
    {
        UniTask<Sprite?> GetAsync(IAvatarAttachment avatarAttachment, CancellationToken ct);
    }
}
