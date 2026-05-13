using Arch.Core;
using DCL.CharacterPreview;

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
        public FacialExpressionsCharacterPreviewController(
            CharacterPreviewView view,
            ICharacterPreviewFactory previewFactory,
            World world,
            CharacterPreviewEventBus characterPreviewEventBus)
            : base(view, previewFactory, world, isPreviewPlatformActive: false, characterPreviewEventBus) { }

        public void SetFace(int eyebrowsIndex, int eyesIndex, int mouthIndex) =>
            previewController?.TrySetFace(eyebrowsIndex, eyesIndex, mouthIndex);
    }
}