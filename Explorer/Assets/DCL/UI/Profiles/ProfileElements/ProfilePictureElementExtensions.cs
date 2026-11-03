using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.ProfileElements
{
    public static class ProfilePictureElementExtensions
    {
        // Opacity of the white frame the uGUI ProfilePictureView lays over the profile color
        private const float FRAME_WHITE_BLEND = 0.2f;

        /// <summary>
        ///     The border is not backed by the background, so its lighter shade is computed rather than left translucent.
        /// </summary>
        public static void SetProfileColor(this VisualElement picture, Color profileColor)
        {
            Color frame = Color.Lerp(profileColor, Color.white, FRAME_WHITE_BLEND);
            picture.style.backgroundColor = profileColor;
            picture.style.borderTopColor = frame;
            picture.style.borderRightColor = frame;
            picture.style.borderBottomColor = frame;
            picture.style.borderLeftColor = frame;
        }
    }
}
