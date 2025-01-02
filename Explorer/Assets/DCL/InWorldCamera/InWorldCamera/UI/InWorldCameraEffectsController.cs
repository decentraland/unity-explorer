using DCL.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.InWorldCamera.UI
{
    public class InWorldCameraEffectsController : IDisposable
    {
        private readonly InWorldCameraEffectsView view;
        private readonly Volume globalVolume;
        private readonly List<IDisposable> disposableEvents = new (10);

        public bool DofEnabled => view.EnabledDof.Value;
        public bool AutoFocusEnabled => view.EnableAutoFocus.Value;
        public float FocusDistance => view.FocusDistance.Value;

        public void SetAutoFocus(float distance, float targetFocusDistance) =>
            view.SetAutoFocus(distance, targetFocusDistance);

        public InWorldCameraEffectsController(Volume effectsVolume, ColorAdjustments colorAdjustments, DepthOfField dof, InWorldCameraEffectsView effectsView)
        {
            view = effectsView;
            globalVolume = effectsVolume;

            SetupColorAdjustments(colorAdjustments);
            SetupDepthOfField(dof);

            Disable();
        }

        public void Dispose()
        {
            foreach (IDisposable @event in disposableEvents)
                @event.Dispose();
        }

        private void SetupColorAdjustments(ColorAdjustments colorAdjustments)
        {
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

        private void SetupDepthOfField(DepthOfField depthOfField)
        {
            depthOfField.mode.Override(DepthOfFieldMode.Bokeh);
            depthOfField.active = view.EnabledDof.Value;
            depthOfField.focusDistance.Override(view.FocusDistance.Value);
            depthOfField.focalLength.Override(view.FocalLength.Value);
            depthOfField.aperture.Override(view.Aperture.Value);

            disposableEvents.AddRange(new[]
            {
                view.EnabledDof.Subscribe(value => depthOfField.active = value),
                view.FocusDistance.Subscribe(value =>
                {
                    if (DofEnabled)
                        depthOfField.focusDistance.value = value;
                }),
                view.FocalLength.Subscribe(value => depthOfField.focalLength.value = value),
                view.Aperture.Subscribe(value => depthOfField.aperture.value = value),
            });
        }

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
