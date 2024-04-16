using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using System;
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

        public ApplyCinemachineSettingsSystem(World world, DebugContainerBuilder debugBuilder) : base(world)
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
            float mMaxSpeed = currentSens / 100f;
            cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.m_MaxSpeed = mMaxSpeed;
            cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.m_MaxSpeed = mMaxSpeed;
            cinemachinePreset.FirstPersonCameraData.Noise.m_AmplitudeGain = cameraNoise ? noiseSlider.Value : 0;
            cinemachinePreset.ThirdPersonCameraData.Camera.m_XAxis.m_MaxSpeed = mMaxSpeed;
            cinemachinePreset.ThirdPersonCameraData.Camera.m_YAxis.m_MaxSpeed = mMaxSpeed / 100f;
        }
    }
}
