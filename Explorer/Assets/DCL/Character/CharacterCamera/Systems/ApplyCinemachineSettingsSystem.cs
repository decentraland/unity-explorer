using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.Character.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class ApplyCinemachineSettingsSystem : BaseUnityLoopSystem
    {
        private const string PPREF_SENS = "CameraSensitivity";

        private readonly ElementBinding<float> sensitivitySlider;
        private readonly ElementBinding<float> noiseSlider;

        private float currentSens;
        private bool cameraNoise;

        public ApplyCinemachineSettingsSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            currentSens = PlayerPrefs.GetFloat(PPREF_SENS, 10);
            sensitivitySlider = new ElementBinding<float>(currentSens);
            noiseSlider = new ElementBinding<float>(0.5f);

            debugBuilder.AddWidget("Camera")
                        .AddFloatSliderField("Sensitivity", sensitivitySlider, 0.01f, 100f)
                        .AddToggleField("Enable Noise", OnNoiseChange, false)
                        .AddFloatSliderField("Noise Value", noiseSlider, 0, 20);
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
                PlayerPrefs.SetFloat(PPREF_SENS, currentSens);
            }

            UpdateCameraSettingsQuery(World);
        }

        [Query]
        private void UpdateCameraSettings(ref ICinemachinePreset cinemachinePreset)
        {
            float mMaxSpeed = currentSens / 100f; // for UX reasons we left the sensitivity value to be shown as 0 to 100 so we divide back
            float tpsVerticalMaxSpeed = mMaxSpeed / 100; // third person camera Y values go from 0 to 1, so we approximately divide by 100 again

            cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.m_MaxSpeed = mMaxSpeed;
            cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.m_MaxSpeed = mMaxSpeed;
            cinemachinePreset.FirstPersonCameraData.Noise.m_AmplitudeGain = cameraNoise ? noiseSlider.Value : 0;
            cinemachinePreset.DroneViewCameraData.Camera.m_XAxis.m_MaxSpeed = mMaxSpeed;
            cinemachinePreset.DroneViewCameraData.Camera.m_YAxis.m_MaxSpeed = tpsVerticalMaxSpeed;
            cinemachinePreset.ThirdPersonCameraData.Camera.m_XAxis.m_MaxSpeed = mMaxSpeed;
            cinemachinePreset.ThirdPersonCameraData.Camera.m_YAxis.m_MaxSpeed = tpsVerticalMaxSpeed;
        }
    }
}
