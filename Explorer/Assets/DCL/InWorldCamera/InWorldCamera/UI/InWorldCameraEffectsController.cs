using Arch.Core;
using DCL.Utilities;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Utility;

namespace DCL.InWorldCamera.UI
{

    public class InWorldCameraEffectsController : IDisposable
    {
        private const string AUTO_VOLUME_NAME = "InWorldCamera.GlobalVolume";

        private readonly InWorldCameraEffectsView view;

        // private readonly Camera camera;

        private readonly Volume globalVolume;
        private readonly ColorAdjustments colorAdjustments;
        private readonly DepthOfField depthOfField;

        private float targetFocusDistance;

        public InWorldCameraEffectsController()
        {
            var volume = new GameObject(AUTO_VOLUME_NAME);
            view = volume.AddComponent<InWorldCameraEffectsView>();
            targetFocusDistance = view.FocusDistance;

            globalVolume = volume.AddComponent<Volume>();
            globalVolume.isGlobal = true;
            globalVolume.priority = 100f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            globalVolume.profile = profile;

            colorAdjustments = profile.Add<ColorAdjustments>();

            colorAdjustments.postExposure.Override(view.PostExposure);
            colorAdjustments.contrast.Override(view.Contrast);
            colorAdjustments.saturation.Override(view.Saturation);
            colorAdjustments.hueShift.Override(view.HueShift);
            colorAdjustments.colorFilter.Override(view.FilterColor);

            view.PostExposure.Subscribe(value => colorAdjustments.postExposure.value = value);
            view.Contrast.Subscribe(value => colorAdjustments.contrast.value = value);
            view.Saturation.Subscribe(value => colorAdjustments.saturation.value = value);
            view.HueShift.Subscribe(value => colorAdjustments.hueShift.value = value);
            view.FilterColor.Subscribe(value => colorAdjustments.colorFilter.value = Color.Lerp(Color.white, value, view.FilterIntensity / 100f));

            // // Setup Depth of Field
            // depthOfField = profile.Add<DepthOfField>();
            // depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
            // depthOfField.focusDistance.Override(10f);
            // depthOfField.focalLength.Override(50f);
            // depthOfField.aperture.Override(5.6f);

            Disable();
        }

        public void Dispose()
        {
            globalVolume.profile.SelfDestroy();
            globalVolume.gameObject.SelfDestroy();

            view.PostExposure.Unsubscribe(value => colorAdjustments.postExposure.value = value);
            view.Contrast.Unsubscribe(value => colorAdjustments.contrast.value = value);
            view.Saturation.Unsubscribe(value => colorAdjustments.saturation.value = value);
            view.HueShift.Unsubscribe(value => colorAdjustments.hueShift.value = value);
            view.FilterColor.Unsubscribe(value => colorAdjustments.colorFilter.value = Color.Lerp(Color.white, value, view.FilterIntensity / 100f));
        }

        // void UpdateDepthOfField()
        // {
        //     if (depthOfField != null)
        //     {
        //         depthOfField.active = enableDOF;
        //
        //         if (enableDOF)
        //         {
        //             depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
        //             depthOfField.focusDistance.value = focusDistance;
        //             depthOfField.focalLength.value = focalLength;
        //             depthOfField.aperture.value = aperture;
        //         }
        //     }
        // }

        // UpdateDepthOfField();
        // UpdateAutofocus();

        public void Enable()
        {
            globalVolume.enabled = true;
            view.Show();
        }

        public void Disable()
        {
            globalVolume.enabled = false;
            view.Hide();
        }
    }
}
