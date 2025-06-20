using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Prefs;
using DCL.Settings.Settings;
using ECS.Abstract;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Character.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class ApplyCinemachineSettingsSystem : BaseUnityLoopSystem
    {
        private readonly ElementBinding<float> sensitivitySlider;
        private readonly ElementBinding<float> noiseSlider;
        private readonly ElementBinding<float> minAltitude;
        private readonly ElementBinding<float> maxAltitude;
        private readonly ElementBinding<float> minDistance;
        private readonly ElementBinding<float> maxDistance;
        private readonly ElementBinding<float> currentDistance;
        private readonly ControlsSettingsAsset controlsSettingsAsset;

        private float currentSens;
        private bool cameraNoise;

        private bool isDebug;

        public ApplyCinemachineSettingsSystem(World world, IDebugContainerBuilder debugBuilder, ControlsSettingsAsset controlsSettingsAsset, bool isDebug) : base(world)
        {
            currentSens = DCLPlayerPrefs.GetFloat(DCLPrefKeys.CAMERA_SENSITIVITY, 10);

            sensitivitySlider = new ElementBinding<float>(currentSens);
            noiseSlider = new ElementBinding<float>(0.5f);

            this.controlsSettingsAsset = controlsSettingsAsset;
            this.isDebug = isDebug;

            DebugWidgetBuilder? widget = debugBuilder.TryAddWidget("Camera");

            widget?.AddFloatSliderField("Sensitivity", sensitivitySlider, 0.01f, 100f)
                   .AddToggleField("Enable Noise", OnNoiseChange, false)
                   .AddFloatSliderField("Noise Value", noiseSlider, 0, 20);

            if (isDebug)
            {
                minAltitude = new ElementBinding<float>(0);
                maxAltitude = new ElementBinding<float>(0);
                minDistance = new ElementBinding<float>(0);
                maxDistance = new ElementBinding<float>(0);
                currentDistance = new ElementBinding<float>(0);

                widget?.AddFloatField("Min Draw Dist. Altitude", minAltitude)
                       .AddFloatField("Max Draw Dist. Altitude", maxAltitude)
                       .AddFloatField("Min Draw Dist.", minDistance)
                       .AddFloatField("Max Draw Dist.", maxDistance)
                       .AddFloatField("Current Draw Dist.", currentDistance);
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            if (this.isDebug) InitializeFarClipPlaneControls();
        }

        private void InitializeFarClipPlaneControls()
        {
            var cinemachinePreset = World.Get<ICinemachinePreset>(World.CacheCamera());

            var farClipSettings = cinemachinePreset.FarClipPlaneSettings;
            minAltitude.Value = farClipSettings.MinFarClipPlaneAltitude;
            maxAltitude.Value = farClipSettings.MaxFarClipPlaneAltitude;
            minDistance.Value = farClipSettings.MinFarClipPlane;
            maxDistance.Value = farClipSettings.MaxFarClipPlane;
        }

        private void OnNoiseChange(ChangeEvent<bool> evt)
        {
            cameraNoise = evt.newValue;
        }

        protected override void Update(float t)
        {
            if (!Mathf.Approximately(sensitivitySlider.Value, currentSens))
            {
                currentSens = sensitivitySlider.Value;
                DCLPlayerPrefs.SetFloat(DCLPrefKeys.CAMERA_SENSITIVITY, currentSens);
            }

            UpdateCameraSettingsQuery(World);
        }

        [Query]
        private void UpdateCameraSettings(ref ICinemachinePreset cinemachinePreset)
        {
            float mMaxSpeed = currentSens / 100f; // for UX reasons we left the sensitivity value to be shown as 0 to 100 so we divide back
            float tpsVerticalMaxSpeed = mMaxSpeed / 100; // third person camera Y values go from 0 to 1, so we approximately divide by 100 again

            cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.m_MaxSpeed = mMaxSpeed * controlsSettingsAsset.HorizontalMouseSensitivity;
            cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.m_MaxSpeed = mMaxSpeed * controlsSettingsAsset.VerticalMouseSensitivity;
            cinemachinePreset.FirstPersonCameraData.Noise.m_AmplitudeGain = cameraNoise ? noiseSlider.Value : 0;

            cinemachinePreset.DroneViewCameraData.Camera.m_XAxis.m_MaxSpeed = mMaxSpeed * controlsSettingsAsset.HorizontalMouseSensitivity;
            cinemachinePreset.DroneViewCameraData.Camera.m_YAxis.m_MaxSpeed = tpsVerticalMaxSpeed * controlsSettingsAsset.VerticalMouseSensitivity;

            cinemachinePreset.ThirdPersonCameraData.Camera.m_XAxis.m_MaxSpeed = mMaxSpeed * controlsSettingsAsset.HorizontalMouseSensitivity;
            cinemachinePreset.ThirdPersonCameraData.Camera.m_YAxis.m_MaxSpeed = tpsVerticalMaxSpeed * controlsSettingsAsset.VerticalMouseSensitivity;

            if (isDebug)
            {
                CameraFarClipPlaneSettings farClipSettings = cinemachinePreset.FarClipPlaneSettings;
                farClipSettings.MinFarClipPlaneAltitude = minAltitude.Value;
                farClipSettings.MaxFarClipPlaneAltitude = maxAltitude.Value;
                farClipSettings.MinFarClipPlane = minDistance.Value;
                farClipSettings.MaxFarClipPlane = maxDistance.Value;

                currentDistance.SetAndUpdate(cinemachinePreset.Brain.ActiveVirtualCamera?.State.Lens.FarClipPlane ?? -1);
            }
        }
    }
}
