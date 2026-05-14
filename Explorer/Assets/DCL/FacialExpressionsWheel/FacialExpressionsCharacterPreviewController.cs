using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.CharacterPreview;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.FacialExpressionsWheel
{
    /// <summary>
    ///     Character preview for the facial expressions wheel. Wraps the shared preview infra
    ///     and exposes a single <see cref="SetFace"/> entry point so the wheel controller
    ///     can write resting eyebrow/eye/mouth slice indices without touching ECS directly.
    ///     No platform, no emote events, no wearable bus.
    /// </summary>
    public class FacialExpressionsCharacterPreviewController : CharacterPreviewControllerBase
    {
        private readonly List<URN> shortenedWearables = new ();
        private readonly World ownWorld;

        public FacialExpressionsCharacterPreviewController(
            CharacterPreviewView view,
            ICharacterPreviewFactory previewFactory,
            World world,
            CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, isPreviewPlatformActive: false, characterPreviewEventBus)
        {
            ownWorld = world;
        }

        public override void Initialize(Avatar avatar, Vector3 position)
        {
            shortenedWearables.Clear();

            foreach (URN urn in avatar.Wearables)
                shortenedWearables.Add(urn.Shorten());

            previewAvatarModel.Wearables = shortenedWearables;

            base.Initialize(avatar, position);
        }

        public void SetFace(int eyebrowsIndex, int eyesIndex, int mouthIndex) =>
            previewController?.TrySetFace(eyebrowsIndex, eyesIndex, mouthIndex);

        /// <summary>
        ///     Same as <see cref="SetFace"/> but waits until <see cref="AvatarFaceComponent"/> is attached
        ///     to the preview entity, so the initial seed (called right after <c>OnShow</c> while
        ///     wearables are still instantiating async) actually lands instead of silently no-opping.
        /// </summary>
        public async UniTask SetFaceWhenReadyAsync(int eyebrowsIndex, int eyesIndex, int mouthIndex, CancellationToken ct)
        {
            await UniTask.WaitUntil(IsFaceReady, cancellationToken: ct);

            previewController?.TrySetFace(eyebrowsIndex, eyesIndex, mouthIndex);
        }

        private bool IsFaceReady()
        {
            if (!previewController.HasValue) return false;

            Entity entity = previewController.Value.PreviewEntity;
            return ownWorld.IsAlive(entity) && ownWorld.Has<AvatarFaceComponent>(entity);
        }
    }
}