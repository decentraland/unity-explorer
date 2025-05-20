using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Settings.Settings;
using ECS.Abstract;
using UnityEngine.UIElements;

namespace DCL.Character.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class ApplyCinemachineSettingsSystem : BaseUnityLoopSystem
    {
        private readonly ElementBinding<float> noiseSlider;
        private readonly ControlsSettingsAsset controlsSettingsAsset;

        private bool cameraNoise;

        public ApplyCinemachineSettingsSystem(World world, IDebugContainerBuilder debugBuilder, ControlsSettingsAsset controlsSettingsAsset) : base(world)
        {
            noiseSlider = new ElementBinding<float>(0.5f);
            this.controlsSettingsAsset = controlsSettingsAsset;

            debugBuilder.TryAddWidget("Camera")
                        ?.AddToggleField("Enable Noise", OnNoiseChange, false)
                        .AddFloatSliderField("Noise Value", noiseSlider, 0, 20);
        }

        private void OnNoiseChange(ChangeEvent<bool> evt)
        {
            cameraNoise = evt.newValue;
        }

        protected override void Update(float t)
        {
            UpdateCameraSettingsQuery(World);
        }

        [Query]
        private void UpdateCameraSettings(ref ICinemachinePreset cinemachinePreset)
        {
            cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.m_MaxSpeed = controlsSettingsAsset.MaxSpeed * controlsSettingsAsset.HorizontalMouseSensitivity;
            cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.m_MaxSpeed = controlsSettingsAsset.MaxSpeed * controlsSettingsAsset.VerticalMouseSensitivity;
            cinemachinePreset.FirstPersonCameraData.Noise.m_AmplitudeGain = cameraNoise ? noiseSlider.Value : 0;
        }
    }
}
