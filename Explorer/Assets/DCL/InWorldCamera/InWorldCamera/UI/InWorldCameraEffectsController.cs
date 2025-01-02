using Arch.Core;
using DCL.Utilities;
using System;
using System.Collections.Generic;
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
        private ColorAdjustments colorAdjustments;
        private DepthOfField depthOfField;

        private float targetFocusDistance;
        private readonly List<IDisposable> disposableEvents = new (10);

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

            SetupColorAdjustments(profile);
            SetupDepthOfField(profile);

            Disable();
        }

        private void SetupColorAdjustments(VolumeProfile profile)
        {
            colorAdjustments = profile.Add<ColorAdjustments>();

            colorAdjustments.postExposure.Override(view.PostExposure);
            colorAdjustments.contrast.Override(view.Contrast);
            colorAdjustments.saturation.Override(view.Saturation);
            colorAdjustments.hueShift.Override(view.HueShift);
            colorAdjustments.colorFilter.Override(view.FilterColor);

            disposableEvents.AddRange(new[]
            {
                view.PostExposure.Subscribe(value => colorAdjustments.postExposure.value = value),
                view.Contrast.Subscribe(value => colorAdjustments.contrast.value = value),
                view.Saturation.Subscribe(value => colorAdjustments.saturation.value = value),
                view.HueShift.Subscribe(value => colorAdjustments.hueShift.value = value),
                view.FilterColor.Subscribe(value => colorAdjustments.colorFilter.value = Color.Lerp(Color.white, value, view.FilterIntensity / 100f)),
            });
        }

        private void SetupDepthOfField(VolumeProfile profile)
        {
            depthOfField = profile.Add<DepthOfField>();

            depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
            depthOfField.active = view.EnabledDof.Value;
            depthOfField.focusDistance.Override(view.FocusDistance.Value);
            depthOfField.focalLength.Override(view.FocalLength.Value);
            depthOfField.aperture.Override(view.Aperture.Value);

            disposableEvents.AddRange(new[]
            {
                view.EnabledDof.Subscribe(value => depthOfField.active = value),
                view.FocusDistance.Subscribe(value => depthOfField.focusDistance.value = value),
                view.FocalLength.Subscribe(value => depthOfField.focalLength.value = value),
                view.Aperture.Subscribe(value => depthOfField.aperture.value = value),
            });
        }

        public void Dispose()
        {
            globalVolume.profile.SelfDestroy();
            globalVolume.gameObject.SelfDestroy();

            foreach (IDisposable @event in disposableEvents)
                @event.Dispose();
        }

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
