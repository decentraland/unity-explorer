using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Utility;

namespace DCL.InWorldCamera.UI
{
    public class InWorldCameraEffectsController : IDisposable
    {
        private readonly Camera camera;
        private const string AUTO_VOLUME_NAME = "InWorldCamera.GlobalVolume";

        private readonly Volume globalVolume;
        private readonly ColorAdjustments colorAdjustments;
        private readonly DepthOfField depthOfField;

        private float targetFocusDistance;

        public InWorldCameraEffectsController(Camera camera)
        {
            this.camera = camera;

            globalVolume = new GameObject(AUTO_VOLUME_NAME).AddComponent<Volume>();
            globalVolume.isGlobal = true;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            globalVolume.profile = profile;

            // Setup Color Adjustments with all parameters
            colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.postExposure.Override(0f);
            colorAdjustments.contrast.Override(0f);
            colorAdjustments.saturation.Override(0f);
            colorAdjustments.hueShift.Override(0f);
            colorAdjustments.colorFilter.Override(Color.white);

            // Setup Depth of Field
            depthOfField = profile.Add<DepthOfField>();
            depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
            depthOfField.focusDistance.Override(10f);
            depthOfField.focalLength.Override(50f);
            depthOfField.aperture.Override(5.6f);

            // TODO:
            // targetFocusDistance = focusDistance;
        }

        public void Dispose()
        {
            globalVolume.profile.SelfDestroy();
            globalVolume.gameObject.SelfDestroy();
        }
    }
}
