using DCL.Multiplayer.Movement.Channel;
using DCL.Utilities.Extensions;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public class MovementPlayground : MonoBehaviour
    {
        [SerializeField] private GameObject local;
        [SerializeField] private GameObject remote;
        [SerializeField] private Vector2 offset = Vector2.right;
        [SerializeField] private ArtificialLatency latency = new ();
        [Header("Interpolation")]
        [SerializeField] private ActiveTweak useInterpolation = new ();
        [SerializeField] private InterpolateRatio interpolateRatio = new ();

        private IMovementInputChannel inputChannel;
        private IMovementOutputChannel outputChannel;

        private void Start()
        {
            local.EnsureNotNull();
            remote.EnsureNotNull();

            var output = new DemoMovementOutputChannel();

            inputChannel = new DemoMovementInputChannel(latency, output);

            outputChannel = new DebugMovementOutputChannel(
                new InterpolatedMovementOutputChannel(output, interpolateRatio),
                output,
                useInterpolation
            );
        }

        private void Update()
        {
            inputChannel.Send(local.transform.position);
            remote.transform.position = outputChannel.Pose() + offset;
        }

        [Serializable]
        public class ArtificialLatency : IArtificialLatency
        {
            [SerializeField] private float latency;

            public float Latency => latency;
        }

        [Serializable]
        public class InterpolateRatio : InterpolatedMovementOutputChannel.IInterpolateRatio
        {
            [SerializeField] private float value;

            public float Value => value;
        }

        [Serializable]
        public class ActiveTweak : DebugMovementOutputChannel.IActiveTweak
        {
            [SerializeField] private bool active;

            public bool Active => active;
        }
    }
}
