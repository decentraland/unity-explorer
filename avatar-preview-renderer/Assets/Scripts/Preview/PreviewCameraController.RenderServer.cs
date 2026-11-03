#if DCL_RENDER_SERVER
namespace Preview
{
    public partial class PreviewCameraController
    {
        /// <summary>
        /// Ends every zoom lerp at its fitted field of view now, instead of over the next seconds.
        /// </summary>
        public void SnapToTargetFieldOfView()
        {
            SnapToTarget(_avatarFraming);
            SnapToTarget(_wearableFraming);
            SnapToTarget(_builderFraming);
        }

        private static void SnapToTarget(CameraFraming framing) =>
            framing.Camera.Lens.FieldOfView = framing.TargetFOV;
    }
}
#endif
