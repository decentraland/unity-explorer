using UnityEngine;

namespace DCL.Multiplayer.Movement.Channel
{
    public class DebugMovementOutputChannel : IMovementOutputChannel
    {
        private readonly IMovementOutputChannel activeChannel;
        private readonly IMovementOutputChannel defaultChannel;
        private readonly IActiveTweak activeTweak;

        public DebugMovementOutputChannel(IMovementOutputChannel activeChannel, IMovementOutputChannel defaultChannel, IActiveTweak activeTweak)
        {
            this.activeChannel = activeChannel;
            this.defaultChannel = defaultChannel;
            this.activeTweak = activeTweak;
        }

        public Vector2 Pose() =>
            activeTweak.Active
                ? activeChannel.Pose()
                : defaultChannel.Pose();

        public interface IActiveTweak
        {
            bool Active { get; }
        }
    }
}
