using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;

namespace DCL.VoiceChat.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class VoiceChatLoudnessMonitoringSystem : BaseUnityLoopSystem
    {
        private readonly VoiceChatMicrophoneHandler microphoneHandler;

        public VoiceChatLoudnessMonitoringSystem(World world, VoiceChatMicrophoneHandler microphoneHandler) : base(world)
        {
            this.microphoneHandler = microphoneHandler;
        }

        protected override void Update(float t)
        {
            microphoneHandler.CheckLoudnessAndControlAudio();
        }
    }
}
