using UnityEngine;

namespace DCL.CharacterPreview.Components
{
    public struct CharacterPreviewComponent
    {
        public Camera Camera;
        public RectTransform RenderImageRect;
        public AvatarPreviewHeadIKSettings Settings;

        // The preview is not raycast, so the UI hosting it reports the pointer being over the figure here
        public bool IsHovered;
    }
}
