using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Channel
{
    public class DemoMovementInputChannel : IMovementInputChannel
    {
        private readonly IArtificialLatency artificialLatency;
        private readonly DemoMovementOutputChannel demoMovementOutput;

        private float lastUpdated;

        public DemoMovementInputChannel(IArtificialLatency artificialLatency, DemoMovementOutputChannel demoMovementOutput)
        {
            this.artificialLatency = artificialLatency;
            this.demoMovementOutput = demoMovementOutput;
        }

        public void Send(Vector2 pose)
        {
            var current = UnityEngine.Time.time;
            if (current - lastUpdated > artificialLatency.Latency)
            {
                demoMovementOutput.Apply(pose);
                lastUpdated = current;
            }
        }
    }
}
