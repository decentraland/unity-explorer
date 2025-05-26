using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using UnityEngine;

namespace DCL.VoiceChat.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class VoiceChatStatusMonitoringSystem : BaseUnityLoopSystem
    {
        private readonly VoiceChatMicrophoneHandler microphoneHandler;

        public VoiceChatStatusMonitoringSystem(World world, VoiceChatMicrophoneHandler microphoneHandler) : base(world)
        {
            this.microphoneHandler = microphoneHandler;
        }

        protected override void Update(float t)
        {
            // Audio processing and noise gating is now handled by VoiceChatAudioProcessor
            // This system can be used for monitoring voice chat status
            
            // Available properties for monitoring:
            // bool isGateOpen = microphoneHandler.IsNoiseGateOpen;      // Is the noise gate currently open?
            // float currentGain = microphoneHandler.CurrentGain;        // Current AGC gain level
            // bool isTalking = microphoneHandler.IsTalking;             // Is the user in talking mode?
            // float loudness = microphoneHandler.GetCurrentLoudness();  // Raw microphone loudness
            // string micName = microphoneHandler.MicrophoneName;        // Current microphone device name
            
            // Example: Log status every few seconds for debugging
            // if (Time.time % 2f < Time.deltaTime)
            // {
            //     Debug.Log($"Voice Status - Talking: {microphoneHandler.IsTalking}, " +
            //               $"Gate Open: {microphoneHandler.IsNoiseGateOpen}, " +
            //               $"Gain: {microphoneHandler.CurrentGain:F2}");
            // }
        }
    }
} 