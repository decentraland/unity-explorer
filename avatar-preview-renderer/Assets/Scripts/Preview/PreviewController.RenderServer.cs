#if DCL_RENDER_SERVER
using Unity.Cinemachine;
using UnityEngine;

namespace Preview
{
    public partial class PreviewController
    {
        public Camera RenderCamera => mainCamera;

        public float EmoteLength => emoteAnimationController.GetEmoteLength();

        /// <summary>
        /// Makes every camera switch a cut, so the frame after a reload is already framed on its
        /// subject rather than part way through a blend.
        /// </summary>
        public void UseCameraCuts()
        {
            var brain = mainCamera.GetComponent<CinemachineBrain>();

            if (brain == null)
            {
                Debug.LogError("[RenderServer] No CinemachineBrain on the preview camera, blends stay on");
                return;
            }

            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            brain.CustomBlends = null;
        }

        /// <summary>
        /// Freezes the framing a reload settled on: no auto-rotation and no zoom still easing in.
        /// </summary>
        public void HoldFraming()
        {
            avatarRotator.EnableAutoRotate = false;
            wearableRotator.EnableAutoRotate = false;
            previewCameraController.SnapToTargetFieldOfView();
        }

        /// <summary>
        /// Turns the item on its own (<paramref name="itemAlone"/>) or the avatar to an exact angle.
        /// </summary>
        public void Rotate(bool itemAlone, float yaw, float pitch) =>
            (itemAlone ? wearableRotator : avatarRotator).SetAngles(yaw, pitch);

        public void PoseEmoteAt(float seconds) => emoteAnimationController.PoseAt(seconds);

        /// <summary>
        /// A load that throws leaves <see cref="_loading"/> set, which turns every later reload into a
        /// no-op. Clears it so the next reload runs.
        /// </summary>
        public void RecoverFromFailedLoad()
        {
            _loading = false;
            _shouldReload = false;
        }
    }
}
#endif
